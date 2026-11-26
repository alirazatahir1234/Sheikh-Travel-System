using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Reminder rules, the dispatch log and the candidate queries each rule type watches. Every statement filters on TenantId.</summary>
public interface ITravelReminderRepository
{
    Task<IReadOnlyList<TravelReminderRuleRow>> GetRulesAsync(int tenantId, CancellationToken cancellationToken = default);
    /// <summary>Replaces the tenant's rows for one rule type in a single transaction.</summary>
    Task SaveRuleAsync(int tenantId, string ruleType, SaveTravelReminderRuleDto dto, int? userId, CancellationToken cancellationToken = default);

    /// <summary>Entities whose trigger date falls in [from, to] for the rule type; only entities whose trigger is still open.</summary>
    Task<IReadOnlyList<TravelReminderCandidate>> GetCandidatesAsync(int tenantId, string ruleType, DateTime fromUtc, DateTime toUtc,
        TravelBranchScope? scope = null, CancellationToken cancellationToken = default);

    /// <summary>Inserts the log row for the dedupe key; false when another pass already claimed it.</summary>
    Task<long?> TryClaimAsync(int tenantId, TravelReminderClaim claim, CancellationToken cancellationToken = default);
    Task MarkAsync(int tenantId, long id, string status, int? notificationId, string? failureReason, CancellationToken cancellationToken = default);

    Task<PagedResult<TravelReminderLogItemDto>> GetLogAsync(int tenantId, TravelReminderLogFilter filter, int page, int pageSize,
        CancellationToken cancellationToken = default);
    /// <summary>Counts per status since <paramref name="sinceUtc"/>, zero-filled by the caller.</summary>
    Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<int> CountSkippedNoAgentAsync(int tenantId, DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>Active tenants with the travel module (for the scanner).</summary>
    Task<IReadOnlyList<int>> GetActiveTenantIdsAsync(CancellationToken cancellationToken = default);
}
