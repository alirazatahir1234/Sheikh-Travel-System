using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Sub-agents, agent commission rules, payouts and invoice attribution. Every statement filters on TenantId.</summary>
public interface ITravelCommissionRepository
{
    Task<IReadOnlyList<TravelSubAgentDto>> GetSubAgentsAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default);
    Task<bool> SubAgentNameExistsAsync(int tenantId, string name, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> SaveSubAgentAsync(int tenantId, int? id, SaveTravelSubAgentDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSubAgentAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> SubAgentExistsAsync(int tenantId, int id, CancellationToken cancellationToken = default);
    Task<bool> UserExistsAsync(int tenantId, int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelCommissionRuleDto>> GetRulesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<int> SaveRuleAsync(int tenantId, int? id, SaveTravelCommissionRuleDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteRuleAsync(int tenantId, int id, CancellationToken cancellationToken = default);

    /// <summary>Issued invoices attributed to a staff agent or sub-agent (optionally one beneficiary; issue date range applied).</summary>
    Task<IReadOnlyList<TravelAttributedInvoiceFact>> GetAttributedInvoicesAsync(int tenantId, string? beneficiaryType, int? beneficiaryId,
        DateTime? issuedFrom, DateTime? issuedTo, CancellationToken cancellationToken = default);
    /// <summary>The attributed subset of the given invoices (used by profitability so only the contexts in hand are loaded).</summary>
    Task<IReadOnlyList<TravelAttributedInvoiceFact>> GetAttributedInvoicesByIdsAsync(int tenantId, IReadOnlyCollection<int> invoiceIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelCommissionPayoutDto>> GetPayoutsAsync(int tenantId, string? beneficiaryType, int? beneficiaryId, CancellationToken cancellationToken = default);
    /// <summary>Id of the payout already recorded under <paramref name="key"/>, or null (checked before the payable balance).</summary>
    Task<int?> FindPayoutByIdempotencyKeyAsync(int tenantId, Guid key, CancellationToken cancellationToken = default);
    /// <summary>
    /// Records a payout under a per-beneficiary lock: inside the transaction the payouts already recorded for the beneficiary
    /// and currency are summed and the write is refused when <c>paid + Amount</c> would exceed <paramref name="payableCap"/>
    /// (the gross payable commission the handler computed), so two concurrent payouts cannot both pass the balance check.
    /// </summary>
    Task<TravelFinanceWriteResult> RecordPayoutAsync(int tenantId, RecordTravelCommissionPayoutDto dto, decimal payableCap, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task<bool> UpdateAttributionAsync(int tenantId, int invoiceId, int? agentUserId, int? subAgentId, int? userId, CancellationToken cancellationToken = default);
}
