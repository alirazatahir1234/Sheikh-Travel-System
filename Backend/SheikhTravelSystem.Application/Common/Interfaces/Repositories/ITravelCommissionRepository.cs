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
    Task<IReadOnlyList<TravelCommissionPayoutDto>> GetPayoutsAsync(int tenantId, string? beneficiaryType, int? beneficiaryId, CancellationToken cancellationToken = default);
    Task<TravelFinanceWriteResult> RecordPayoutAsync(int tenantId, RecordTravelCommissionPayoutDto dto, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task<bool> UpdateAttributionAsync(int tenantId, int invoiceId, int? agentUserId, int? subAgentId, int? userId, CancellationToken cancellationToken = default);
}
