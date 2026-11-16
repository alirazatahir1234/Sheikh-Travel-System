using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Platform;
using SheikhTravelSystem.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace SheikhTravelSystem.Infrastructure.Services;

public sealed class SubscriptionBillingService(
    IPlatformRepository platformRepository,
    INotificationService notifications,
    ILogger<SubscriptionBillingService> logger) : ISubscriptionBillingService
{
    public Task<bool> AreFeaturesLockedAsync(int tenantId, CancellationToken cancellationToken = default)
        => platformRepository.AreSubscriptionFeaturesLockedAsync(tenantId, cancellationToken);

    public async Task RunBillingCycleForAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await platformRepository.ListActiveTenantIdsForBillingAsync(cancellationToken);
        var asOf = DateTime.UtcNow;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                await platformRepository.SyncTenantSubscriptionInvoicesAsync(tenantId, asOf, cancellationToken);
                var invoiceId = await platformRepository.CreateSubscriptionRenewalInvoiceAsync(tenantId, cancellationToken);
                if (invoiceId is int id)
                    await SendInvoiceEmailAsync(tenantId, id, cancellationToken);

                await platformRepository.RefreshSubscriptionFeatureLockAsync(tenantId, asOf, cancellationToken);
                await platformRepository.ProcessSubscriptionExpiryAndDeactivationAsync(tenantId, asOf, cancellationToken);
                await SendLifecycleEmailsAsync(tenantId, asOf, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Subscription billing cycle failed for tenant {TenantId}", tenantId);
            }
        }
    }

    public async Task<int?> IssueRenewalInvoiceAsync(int tenantId, bool sendEmail, CancellationToken cancellationToken = default)
    {
        var invoiceId = await platformRepository.CreateSubscriptionRenewalInvoiceAsync(tenantId, cancellationToken);
        if (invoiceId is int id && sendEmail)
            await SendInvoiceEmailAsync(tenantId, id, cancellationToken);
        return invoiceId;
    }

    public async Task<bool> MarkInvoicePaidAsync(
        int tenantId, int invoiceId, string? paymentMethod, string? reference, CancellationToken cancellationToken = default)
    {
        var before = await platformRepository.GetTenantSubscriptionAccessAsync(tenantId, cancellationToken);
        var ok = await platformRepository.MarkSubscriptionInvoicePaidAsync(
            tenantId, invoiceId, paymentMethod, reference, cancellationToken);
        if (!ok) return false;

        var after = await platformRepository.GetTenantSubscriptionAccessAsync(tenantId, cancellationToken);
        var recovered = before is not null
            && (string.Equals(before.Value.SubscriptionStatus, SubscriptionBillingLifecycle.StatusPastDue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(before.Value.SubscriptionStatus, SubscriptionBillingLifecycle.StatusExpired, StringComparison.OrdinalIgnoreCase)
                || !before.Value.TenantIsActive)
            && after is not null
            && string.Equals(after.Value.SubscriptionStatus, SubscriptionBillingLifecycle.StatusActive, StringComparison.OrdinalIgnoreCase)
            && after.Value.TenantIsActive;

        if (recovered)
            await SendLifecycleEmailAsync(tenantId, "subscription_reactivated", $"REACTIVATED-{invoiceId}",
                "Subscription reactivated",
                "Your SheikhGo Nexus subscription has been reactivated after payment was verified. Welcome back.",
                NotificationType.PaymentReceived, cancellationToken);

        return true;
    }

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

    private async Task SendLifecycleEmailsAsync(int tenantId, DateTime asOf, CancellationToken cancellationToken)
    {
        var access = await platformRepository.GetTenantSubscriptionAccessAsync(tenantId, cancellationToken);
        if (access is null) return;

        var status = access.Value.SubscriptionStatus;
        if (string.Equals(status, SubscriptionBillingLifecycle.StatusPastDue, StringComparison.OrdinalIgnoreCase))
        {
            await SendLifecycleEmailAsync(tenantId, "subscription_overdue",
                $"OVERDUE-{asOf:yyyyMMdd}",
                "Subscription payment overdue",
                "Your SheikhGo Nexus subscription payment is overdue. Premium modules are locked until payment is verified. Contact billing to restore full access.",
                NotificationType.PaymentReceived, cancellationToken);
        }
        else if (string.Equals(status, SubscriptionBillingLifecycle.StatusExpired, StringComparison.OrdinalIgnoreCase)
                 || !access.Value.TenantIsActive)
        {
            await SendLifecycleEmailAsync(tenantId, "subscription_expired",
                $"EXPIRED-{asOf:yyyyMMdd}",
                "Subscription expired — account deactivated",
                "Your SheikhGo Nexus subscription access has expired and the company account is deactivated. Login is blocked until a platform admin marks a renewal invoice as paid.",
                NotificationType.PaymentReceived, cancellationToken);
        }
        else if (access.Value.AccessExpiresAtUtc is DateTime expires
                 && expires > asOf
                 && (expires - asOf).TotalDays <= 3
                 && string.Equals(status, SubscriptionBillingLifecycle.StatusActive, StringComparison.OrdinalIgnoreCase))
        {
            await SendLifecycleEmailAsync(tenantId, "subscription_renewal_reminder",
                $"REMINDER-{expires:yyyyMMdd}",
                "Subscription renewal reminder",
                $"Your SheikhGo Nexus access expires on {expires:yyyy-MM-dd} UTC (including grace). Please arrange payment to avoid interruption.",
                NotificationType.PaymentReceived, cancellationToken);
        }
    }

    private async Task SendLifecycleEmailAsync(
        int tenantId,
        string templateKey,
        string reference,
        string title,
        string body,
        NotificationType type,
        CancellationToken cancellationToken)
    {
        if (await platformRepository.WasSubscriptionLifecycleEmailSentAsync(tenantId, templateKey, reference, cancellationToken))
            return;

        var email = await platformRepository.ResolveBillingEmailAsync(tenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
            return;

        await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
            UserId: null,
            TenantId: tenantId,
            Title: $"{title} ({reference})",
            Message: body,
            Type: type,
            ReferenceId: null,
            Priority: 2,
            Channel: NotificationChannels.Email,
            Email: email.Trim(),
            Module: "Platform",
            TemplateKey: templateKey,
            SendNow: true,
            Variables: new Dictionary<string, string>
            {
                ["Reference"] = reference,
                ["TenantId"] = tenantId.ToString()
            },
            Channels: [NotificationChannels.Email]), cancellationToken);
    }
}
