using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Payments;

public interface IPaymentGatewayProvider
{
    string ProviderName { get; }

    Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> HandleWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default);
}
