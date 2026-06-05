using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Payments;

internal sealed class JazzCashPaymentGatewayService(
    IConfiguration configuration,
    PaymentGatewayPaymentRecorder paymentRecorder,
    ILogger<JazzCashPaymentGatewayService> logger) : IPaymentGatewayProvider
{
    public string ProviderName => "JazzCash";

    public Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var checkoutUrl = configuration["JazzCash:CheckoutUrl"];
        var merchantId = configuration["JazzCash:MerchantId"];
        var password = configuration["JazzCash:Password"];
        var integritySalt = configuration["JazzCash:IntegritySalt"];
        var returnUrl = BuildReturnUrl(configuration["JazzCash:ReturnUrl"], request.SuccessUrl);

        if (string.IsNullOrWhiteSpace(checkoutUrl)
            || string.IsNullOrWhiteSpace(merchantId)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(integritySalt)
            || string.IsNullOrWhiteSpace(returnUrl))
        {
            return Task.FromResult(new PaymentCheckoutResult(
                false,
                null,
                null,
                "JazzCash is not configured. Add JazzCash:CheckoutUrl, MerchantId, Password, IntegritySalt, and ReturnUrl."));
        }

        var now = DateTime.UtcNow.AddHours(5);
        var transactionRef = $"T{now:yyyyMMddHHmmss}{request.BookingId}";
        var amount = decimal.ToInt64(decimal.Round(request.Amount * 100, 0, MidpointRounding.AwayFromZero)).ToString();

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["pp_Version"] = configuration["JazzCash:Version"] ?? "2.0",
            ["pp_TxnType"] = configuration["JazzCash:TxnType"] ?? "MWALLET",
            ["pp_Language"] = "EN",
            ["pp_MerchantID"] = merchantId,
            ["pp_Password"] = password,
            ["pp_TxnRefNo"] = transactionRef,
            ["pp_Amount"] = amount,
            ["pp_TxnCurrency"] = request.Currency.ToUpperInvariant(),
            ["pp_TxnDateTime"] = now.ToString("yyyyMMddHHmmss"),
            ["pp_BillReference"] = request.BookingId.ToString(),
            ["pp_Description"] = $"Booking #{request.BookingId}",
            ["pp_TxnExpiryDateTime"] = now.AddMinutes(GetExpiryMinutes()).ToString("yyyyMMddHHmmss"),
            ["pp_ReturnURL"] = returnUrl,
            ["ppmpf_1"] = request.BookingId.ToString(),
            ["ppmpf_2"] = ProviderName,
            ["ppmpf_3"] = "",
            ["ppmpf_4"] = "",
            ["ppmpf_5"] = ""
        };

        fields["pp_SecureHash"] = BuildJazzCashHash(fields, integritySalt);

        var url = QueryHelpers.AddQueryString(
            checkoutUrl,
            fields.ToDictionary(x => x.Key, x => (string?)x.Value, StringComparer.Ordinal));
        logger.LogInformation("JazzCash checkout created for booking {BookingId} with ref {TransactionRef}.", request.BookingId, transactionRef);

        return Task.FromResult(new PaymentCheckoutResult(true, url, transactionRef, null));
    }

    public async Task<bool> HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        var integritySalt = configuration["JazzCash:IntegritySalt"];
        if (string.IsNullOrWhiteSpace(integritySalt))
        {
            logger.LogWarning("JazzCash callback received but JazzCash:IntegritySalt is not configured.");
            return false;
        }

        var values = ParsePayload(payload);
        if (!VerifyJazzCashHash(values, integritySalt))
        {
            logger.LogWarning("JazzCash callback hash verification failed.");
            return false;
        }

        var responseCode = Get(values, "pp_ResponseCode");
        if (responseCode != "000")
        {
            logger.LogInformation("JazzCash callback ignored response code {ResponseCode}.", responseCode);
            return true;
        }

        if (!int.TryParse(Get(values, "ppmpf_1", "pp_BillReference"), out var bookingId)
            || string.IsNullOrWhiteSpace(Get(values, "pp_TxnRefNo"))
            || !decimal.TryParse(Get(values, "pp_Amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amountMinor))
        {
            logger.LogWarning("JazzCash callback missing required booking/payment fields.");
            return false;
        }

        return await paymentRecorder.RecordAsync(
            bookingId,
            amountMinor / 100m,
            ProviderName,
            Get(values, "pp_TxnRefNo"),
            "JazzCash payment",
            cancellationToken);
    }

    private string? BuildReturnUrl(string? configuredReturnUrl, string fallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredReturnUrl))
        {
            return configuredReturnUrl;
        }

        var baseUrl = configuration["PortalPaymentGateway:ApiBaseUrl"];
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var apiBase))
        {
            return new Uri(apiBase, "/api/customer-portal/payments/webhook/jazzcash").ToString();
        }

        var portalBaseUrl = configuration["PortalPaymentGateway:ReturnBaseUrl"];
        return Uri.TryCreate(portalBaseUrl, UriKind.Absolute, out var portalBase)
            ? new Uri(portalBase, fallbackPath).ToString()
            : null;
    }

    private int GetExpiryMinutes()
        => int.TryParse(configuration["JazzCash:ExpiryMinutes"], out var minutes) && minutes > 0 ? minutes : 30;

    private static Dictionary<string, StringValues> ParsePayload(string payload)
        => QueryHelpers.ParseQuery(payload.StartsWith('?') ? payload : $"?{payload}");

    private static string BuildJazzCashHash(SortedDictionary<string, string> fields, string integritySalt)
    {
        var data = string.Join("&", fields
            .Where(x => x.Key != "pp_SecureHash" && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Value));
        return Sha256Hex($"{integritySalt}&{data}");
    }

    private static bool VerifyJazzCashHash(Dictionary<string, StringValues> values, string integritySalt)
    {
        var provided = Get(values, "pp_SecureHash");
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!string.Equals(key, "pp_SecureHash", StringComparison.OrdinalIgnoreCase))
            {
                fields[key] = value.ToString();
            }
        }

        return FixedTimeEquals(provided, BuildJazzCashHash(fields, integritySalt));
    }

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

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToUpperInvariant();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left.ToUpperInvariant());
        var rightBytes = Encoding.UTF8.GetBytes(right.ToUpperInvariant());
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
