using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

public interface IWhatsAppAccountResolver
{
    /// <summary>
    /// Resolves an active tenant WhatsApp account.
    /// Explicit <paramref name="whatsAppAccountId"/> wins; otherwise routes by phone; otherwise default.
    /// </summary>
    Task<WhatsAppAccountRow?> ResolveAsync(
        int tenantId,
        int? whatsAppAccountId,
        string? recipientPhone,
        CancellationToken cancellationToken = default);

    Task<WhatsAppAccountRow?> ResolveByCodeAsync(
        int tenantId,
        string accountCode,
        CancellationToken cancellationToken = default);
}

public sealed class WhatsAppAccountResolver(
    IWhatsAppInboxRepository repository,
    IWhatsAppRoutingService routing) : IWhatsAppAccountResolver
{
    public async Task<WhatsAppAccountRow?> ResolveAsync(
        int tenantId,
        int? whatsAppAccountId,
        string? recipientPhone,
        CancellationToken cancellationToken = default)
    {
        if (whatsAppAccountId is > 0)
        {
            var byId = await repository.GetAccountByIdAsync(tenantId, whatsAppAccountId.Value, cancellationToken);
            if (byId is null || !byId.IsActive)
                return null;
            return byId;
        }

        if (!string.IsNullOrWhiteSpace(recipientPhone))
        {
            var code = routing.ResolveAccountCode(recipientPhone);
            var byCode = await ResolveByCodeAsync(tenantId, code, cancellationToken);
            if (byCode is not null)
                return byCode;
        }

        return await ResolveDefaultAsync(tenantId, cancellationToken);
    }

    public async Task<WhatsAppAccountRow?> ResolveByCodeAsync(
        int tenantId,
        string accountCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountCode)) return null;
        var accounts = await repository.ListActiveAccountRowsAsync(cancellationToken);
        return accounts.FirstOrDefault(a =>
            a.TenantId == tenantId
            && a.IsActive
            && string.Equals(a.Code, accountCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<WhatsAppAccountRow?> ResolveDefaultAsync(int tenantId, CancellationToken ct)
    {
        var accounts = (await repository.ListActiveAccountRowsAsync(ct))
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .ToList();
        if (accounts.Count == 0) return null;
        return accounts.FirstOrDefault(a => a.IsDefault)
               ?? accounts.FirstOrDefault(a => string.Equals(a.Code, "UAE", StringComparison.OrdinalIgnoreCase))
               ?? accounts.FirstOrDefault();
    }
}
