using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services;

public sealed class SubscriptionBillingService(
    IPlatformRepository platformRepository,
    INotificationService notifications) : ISubscriptionBillingService
{
    public Task<bool> AreFeaturesLockedAsync(int tenantId, CancellationToken cancellationToken = default)
        => platformRepository.AreSubscriptionFeaturesLockedAsync(tenantId, cancellationToken);

    public async Task RunBillingCycleForAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await platformRepository.ListActiveTenantIdsForBillingAsync(cancellationToken);
        var asOf = DateTime.UtcNow;
        foreach (var tenantId in tenantIds)
        {
            await platformRepository.SyncTenantSubscriptionInvoicesAsync(tenantId, asOf, cancellationToken);
            await platformRepository.RefreshSubscriptionFeatureLockAsync(tenantId, asOf, cancellationToken);
            var invoiceId = await platformRepository.CreateSubscriptionRenewalInvoiceAsync(tenantId, cancellationToken);
            if (invoiceId is int id)
                await SendInvoiceEmailAsync(tenantId, id, cancellationToken);
        }
    }

    public async Task<int?> IssueRenewalInvoiceAsync(int tenantId, bool sendEmail, CancellationToken cancellationToken = default)
    {
        var invoiceId = await platformRepository.CreateSubscriptionRenewalInvoiceAsync(tenantId, cancellationToken);
        if (invoiceId is int id && sendEmail)
            await SendInvoiceEmailAsync(tenantId, id, cancellationToken);
        return invoiceId;
    }

    public Task<bool> MarkInvoicePaidAsync(
        int tenantId, int invoiceId, string? paymentMethod, string? reference, CancellationToken cancellationToken = default)
        => platformRepository.MarkSubscriptionInvoicePaidAsync(tenantId, invoiceId, paymentMethod, reference, cancellationToken);

    public async Task<bool> SendInvoiceEmailAsync(int tenantId, int invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await platformRepository.GetSubscriptionInvoiceAsync(tenantId, invoiceId, cancellationToken);
        if (invoice is null) return false;

        var email = await platformRepository.ResolveBillingEmailAsync(tenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var body = $"""
            Dear SheikhGo Nexus customer,

            Your subscription invoice {invoice.Value.InvoiceNumber} for plan {invoice.Value.PlanName ?? "Nexus"} is ready.
            Amount due: {invoice.Value.Amount:N2} {invoice.Value.CurrencyCode}
            Due date: {(invoice.Value.DueDate?.ToString("yyyy-MM-dd") ?? "—")}

            Pay via your agreed bank transfer method and share the reference with SheikhGo support.
            Online card checkout is not enabled until a payment gateway is configured — see SUBSCRIPTION_BILLING.md.

            Thank you for choosing SheikhGo Nexus.
            """;

        await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
            UserId: null,
            TenantId: tenantId,
            Title: $"Subscription invoice {invoice.Value.InvoiceNumber}",
            Message: body,
            Type: NotificationType.PaymentReceived,
            ReferenceId: invoiceId,
            Priority: 2,
            Channel: NotificationChannels.Email,
            Email: email.Trim(),
            Module: "Platform",
            TemplateKey: "subscription_invoice",
            SendNow: true,
            Variables: new Dictionary<string, string>
            {
                ["InvoiceNumber"] = invoice.Value.InvoiceNumber,
                ["Amount"] = invoice.Value.Amount.ToString("N2"),
                ["CurrencyCode"] = invoice.Value.CurrencyCode,
                ["PlanName"] = invoice.Value.PlanName ?? ""
            },
            Channels: [NotificationChannels.Email]), cancellationToken);

        await platformRepository.RecordSubscriptionInvoiceEmailSentAsync(invoiceId, cancellationToken);
        return true;
    }
}
