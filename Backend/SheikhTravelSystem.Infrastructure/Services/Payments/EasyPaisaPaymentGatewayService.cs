using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Payments;

internal sealed class EasyPaisaPaymentGatewayService(
    IConfiguration configuration,
    PaymentGatewayPaymentRecorder paymentRecorder,
    ILogger<EasyPaisaPaymentGatewayService> logger) : IPaymentGatewayProvider
{
    public string ProviderName => "EasyPaisa";

    public Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var checkoutUrl = configuration["EasyPaisa:CheckoutUrl"];
        var storeId = configuration["EasyPaisa:StoreId"];
        var hashKey = configuration["EasyPaisa:HashKey"];
        var postBackUrl = BuildPostBackUrl(configuration["EasyPaisa:PostBackUrl"], request.SuccessUrl);

        if (string.IsNullOrWhiteSpace(checkoutUrl)
            || string.IsNullOrWhiteSpace(storeId)
            || string.IsNullOrWhiteSpace(hashKey)
            || string.IsNullOrWhiteSpace(postBackUrl))
        {
            return Task.FromResult(new PaymentCheckoutResult(
                false,
                null,
                null,
                "EasyPaisa is not configured. Add EasyPaisa:CheckoutUrl, StoreId, HashKey, and PostBackUrl."));
        }

        var orderRef = $"EP{DateTime.UtcNow:yyyyMMddHHmmss}{request.BookingId}";
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["storeId"] = storeId,
            ["amount"] = request.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            ["postBackURL"] = postBackUrl,
            ["orderRefNum"] = orderRef,
            ["expiryDate"] = DateTime.UtcNow.AddMinutes(GetExpiryMinutes()).ToString("yyyyMMdd HHmmss"),
            ["autoRedirect"] = "1",
            ["paymentMethod"] = configuration["EasyPaisa:PaymentMethod"] ?? "MA_PAYMENT_METHOD",
            ["emailAddr"] = string.IsNullOrWhiteSpace(request.CustomerEmail) ? "portal@customer.local" : request.CustomerEmail,
            ["mobileNum"] = configuration["EasyPaisa:DefaultMobileNumber"] ?? "",
            ["bookingId"] = request.BookingId.ToString()
        };

        fields["merchantHashedReq"] = BuildEasyPaisaHash(fields, hashKey);

        var url = QueryHelpers.AddQueryString(
            checkoutUrl,
            fields.ToDictionary(x => x.Key, x => (string?)x.Value, StringComparer.Ordinal));
        logger.LogInformation("EasyPaisa checkout created for booking {BookingId} with ref {OrderRef}.", request.BookingId, orderRef);

        return Task.FromResult(new PaymentCheckoutResult(true, url, orderRef, null));
    }

    public async Task<bool> HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        var hashKey = configuration["EasyPaisa:HashKey"];
        if (string.IsNullOrWhiteSpace(hashKey))
        {
            logger.LogWarning("EasyPaisa callback received but EasyPaisa:HashKey is not configured.");
            return false;
        }

        var values = ParsePayload(payload);
        if (!VerifyEasyPaisaHash(values, hashKey))
        {
            logger.LogWarning("EasyPaisa callback hash verification failed.");
            return false;
        }

        var status = Get(values, "status", "transactionStatus", "responseCode");
        if (!IsSuccessfulStatus(status))
        {
            logger.LogInformation("EasyPaisa callback ignored status {Status}.", status);
            return true;
        }

        if (!int.TryParse(Get(values, "bookingId"), out var bookingId)
            || string.IsNullOrWhiteSpace(Get(values, "orderRefNum"))
            || !decimal.TryParse(Get(values, "amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            logger.LogWarning("EasyPaisa callback missing required booking/payment fields.");
            return false;
        }

        return await paymentRecorder.RecordAsync(
            bookingId,
            amount,
            ProviderName,
            Get(values, "orderRefNum"),
            "EasyPaisa payment",
            cancellationToken);
    }

    private string? BuildPostBackUrl(string? configuredPostBackUrl, string fallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPostBackUrl))
        {
            return configuredPostBackUrl;
        }

        var baseUrl = configuration["PortalPaymentGateway:ApiBaseUrl"];
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var apiBase))
        {
            return new Uri(apiBase, "/api/customer-portal/payments/webhook/easypaisa").ToString();
        }

        var portalBaseUrl = configuration["PortalPaymentGateway:ReturnBaseUrl"];
        return Uri.TryCreate(portalBaseUrl, UriKind.Absolute, out var portalBase)
            ? new Uri(portalBase, fallbackPath).ToString()
            : null;
    }

    private int GetExpiryMinutes()
        => int.TryParse(configuration["EasyPaisa:ExpiryMinutes"], out var minutes) && minutes > 0 ? minutes : 30;

    private static Dictionary<string, StringValues> ParsePayload(string payload)
        => QueryHelpers.ParseQuery(payload.StartsWith('?') ? payload : $"?{payload}");

    private static string BuildEasyPaisaHash(SortedDictionary<string, string> fields, string hashKey)
    {
        var data = string.Join("&", fields
            .Where(x => !string.Equals(x.Key, "merchantHashedReq", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{x.Key}={x.Value}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(hashKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToUpperInvariant();
    }

    private static bool VerifyEasyPaisaHash(Dictionary<string, StringValues> values, string hashKey)
    {
        var provided = Get(values, "merchantHashedReq", "hash", "signature");
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!string.Equals(key, "merchantHashedReq", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "hash", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "signature", StringComparison.OrdinalIgnoreCase))
            {
                fields[key] = value.ToString();
            }
        }

        return FixedTimeEquals(provided, BuildEasyPaisaHash(fields, hashKey));
    }

    private static bool IsSuccessfulStatus(string status)
        => string.Equals(status, "0000", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "000", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "SUCCESSFUL", StringComparison.OrdinalIgnoreCase);

    private static string Get(Dictionary<string, StringValues> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value))
            {
                return value.ToString();
            }
        }

        return "";
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left.ToUpperInvariant());
        var rightBytes = Encoding.UTF8.GetBytes(right.ToUpperInvariant());
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
