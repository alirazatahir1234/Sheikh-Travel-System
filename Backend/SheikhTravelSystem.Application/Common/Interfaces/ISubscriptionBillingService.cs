namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ISubscriptionBillingService
{
    Task<bool> AreFeaturesLockedAsync(int tenantId, CancellationToken cancellationToken = default);

    Task RunBillingCycleForAllTenantsAsync(CancellationToken cancellationToken = default);

    Task<int?> IssueRenewalInvoiceAsync(int tenantId, bool sendEmail, CancellationToken cancellationToken = default);

    Task<bool> MarkInvoicePaidAsync(
        int tenantId,
        int invoiceId,
        string? paymentMethod,
        string? reference,
        CancellationToken cancellationToken = default);

    Task<bool> SendInvoiceEmailAsync(int tenantId, int invoiceId, CancellationToken cancellationToken = default);
}
