using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Payments;
using Stripe;
using Stripe.Checkout;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Stripe checkout adapter. Set Stripe:SecretKey and Stripe:WebhookSecret to enable live payments.
/// </summary>
internal sealed class StripePaymentGatewayService(
    IConfiguration configuration,
    PaymentGatewayPaymentRecorder paymentRecorder,
    ILogger<StripePaymentGatewayService> logger) : IPaymentGatewayProvider
{
    public string ProviderName => "Stripe";

    public async Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var secret = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return new PaymentCheckoutResult(
                false, null, null, "Stripe is not configured. Add Stripe:SecretKey to configuration.");
        }

        if (!TryBuildReturnUrl(request.SuccessUrl, out var successUrl)
            || !TryBuildReturnUrl(request.CancelUrl, out var cancelUrl))
        {
            return new PaymentCheckoutResult(
                false,
                null,
                null,
                "Stripe checkout requires absolute SuccessUrl/CancelUrl or PortalPaymentGateway:ReturnBaseUrl.");
        }

        logger.LogInformation(
            "Stripe checkout requested for booking {BookingId}, amount {Amount} {Currency}",
            request.BookingId, request.Amount, request.Currency);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = AddCheckoutSessionPlaceholder(successUrl),
            CancelUrl = cancelUrl,
            CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail,
            ClientReferenceId = request.BookingId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["bookingId"] = request.BookingId.ToString(),
                ["source"] = "customer-portal"
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = ToMinorUnits(request.Amount, request.Currency),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Booking #{request.BookingId}"
                        }
                    }
                }
            ]
        };

        try
        {
            var service = new SessionService();
            var session = await service.CreateAsync(
                options,
                new RequestOptions { ApiKey = secret },
                cancellationToken);

            return new PaymentCheckoutResult(true, session.Url, session.Id, null);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe checkout creation failed for booking {BookingId}.", request.BookingId);
            return new PaymentCheckoutResult(false, null, null, ex.StripeError?.Message ?? ex.Message);
        }
    }

    public async Task<bool> HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        var webhookSecret = configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            logger.LogWarning("Stripe webhook received but Stripe:WebhookSecret is not configured.");
            return false;
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            return false;
        }

        if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
        {
            logger.LogInformation("Stripe webhook ignored event type {EventType}.", stripeEvent.Type);
            return true;
        }

        if (stripeEvent.Data.Object is not Session session)
        {
            logger.LogWarning("Stripe checkout.session.completed payload did not contain a session.");
            return false;
        }

        return await RecordCompletedCheckoutAsync(session, cancellationToken);
    }

    private async Task<bool> RecordCompletedCheckoutAsync(Session session, CancellationToken cancellationToken)
    {
        if (!TryGetBookingId(session, out var bookingId))
        {
            logger.LogWarning("Stripe session {SessionId} is missing booking metadata.", session.Id);
            return false;
        }

        var amount = FromMinorUnits(session.AmountTotal ?? 0, session.Currency ?? "pkr");
        if (amount <= 0)
        {
            logger.LogWarning("Stripe session {SessionId} has invalid amount {Amount}.", session.Id, amount);
            return false;
        }

        var recorded = await paymentRecorder.RecordAsync(
            bookingId,
            amount,
            ProviderName,
            session.Id,
            "Stripe Checkout payment",
            cancellationToken);

        if (recorded)
            logger.LogInformation("Recorded Stripe checkout {SessionId} for booking {BookingId}.", session.Id, bookingId);
        else
            logger.LogWarning("Could not record Stripe checkout {SessionId} for booking {BookingId}.", session.Id, bookingId);

        return recorded;
    }

    private bool TryBuildReturnUrl(string value, out string url)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            url = absolute.ToString();
            return true;
        }

        var baseUrl = configuration["PortalPaymentGateway:ReturnBaseUrl"];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            url = "";
            return false;
        }

        url = new Uri(baseUri, value).ToString();
        return true;
    }

    private static string AddCheckoutSessionPlaceholder(string successUrl)
    {
        var separator = successUrl.Contains('?') ? "&" : "?";
        return successUrl.Contains("{CHECKOUT_SESSION_ID}", StringComparison.Ordinal)
            ? successUrl
            : $"{successUrl}{separator}session_id={{CHECKOUT_SESSION_ID}}";
    }

    private static long ToMinorUnits(decimal amount, string currency)
    {
        return IsZeroDecimalCurrency(currency)
            ? decimal.ToInt64(decimal.Round(amount, 0, MidpointRounding.AwayFromZero))
            : decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero));
    }

    private static decimal FromMinorUnits(long amount, string currency)
    {
        return IsZeroDecimalCurrency(currency) ? amount : amount / 100m;
    }

    private static bool IsZeroDecimalCurrency(string currency)
    {
        return ZeroDecimalCurrencies.Contains(currency.ToUpperInvariant());
    }

    private static bool TryGetBookingId(Session session, out int bookingId)
    {
        if (session.Metadata != null
            && session.Metadata.TryGetValue("bookingId", out var metadataBookingId)
            && int.TryParse(metadataBookingId, out bookingId))
        {
            return true;
        }

        return int.TryParse(session.ClientReferenceId, out bookingId);
    }

    private static readonly HashSet<string> ZeroDecimalCurrencies =
    [
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF",
        "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    ];
}
