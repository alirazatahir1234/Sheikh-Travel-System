using Microsoft.Extensions.Configuration;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Payments;

internal sealed class ConfiguredPaymentGatewayService(
    IConfiguration configuration,
    IEnumerable<IPaymentGatewayProvider> providers) : IPaymentGatewayService
{
    private IPaymentGatewayProvider CurrentProvider => providers.FirstOrDefault(p =>
        string.Equals(p.ProviderName, configuration["PortalPaymentGateway:Provider"] ?? "Stripe", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("Configured payment provider is not registered.");

    public string ProviderName => CurrentProvider.ProviderName;

    public Task<PaymentCheckoutResult> CreateCheckoutSessionAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
        => CurrentProvider.CreateCheckoutSessionAsync(request, cancellationToken);

    public Task<bool> HandleWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default)
        => CurrentProvider.HandleWebhookAsync(payload, signature, cancellationToken);
}
