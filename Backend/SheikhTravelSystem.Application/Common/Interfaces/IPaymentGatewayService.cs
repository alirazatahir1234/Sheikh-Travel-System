namespace SheikhTravelSystem.Application.Common.Interfaces;

public record PaymentCheckoutRequest(
    int BookingId,
    decimal Amount,
    string Currency,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl);

public record PaymentCheckoutResult(bool Success, string? CheckoutUrl, string? SessionId, string? Error);

public interface IPaymentGatewayService
{
    string ProviderName { get; }
    Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<bool> HandleWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);
}
