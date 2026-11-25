using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Visa cases, applicants and history. Every statement is scoped by <c>tenantId</c>; list/get/summary also apply
/// the caller's branch scope. Writes that depend on status compare the expected status (optimistic concurrency)
/// and write the history row in the same transaction.
/// </summary>
public interface ITravelVisaCaseRepository
{
    Task<PagedResult<TravelVisaCaseListItemDto>> GetPagedAsync(
        int tenantId, TravelBranchScope scope, TravelVisaCaseListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<TravelVisaCaseDto?> GetByIdAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<TravelVisaCaseState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);

    /// <summary>Applicants with raw passport numbers (handlers mask).</summary>
    Task<IReadOnlyList<TravelVisaApplicantDto>> GetApplicantsAsync(int caseId, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelVisaHistoryDto>> GetHistoryAsync(int caseId, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetStatusCountsAsync(int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelVisaCaseListItemDto>> GetByCustomerAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Allocates the case number, inserts as Draft and writes the first history row in one transaction.</summary>
    Task<int> CreateAsync(int tenantId, CreateTravelVisaCaseDto dto, string countryCode, int? branchId, int? userId, DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Updates details of a non-terminal case; false when missing or terminal.</summary>
    Task<bool> UpdateAsync(int id, int tenantId, UpdateTravelVisaCaseDto dto, CancellationToken cancellationToken = default);

    Task<bool> ChangeStatusAsync(int tenantId, TravelVisaCaseStatusChange change, int? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds travelers of the case's own customer that are not yet applicants, only while the case is in
    /// <paramref name="expectedStatus"/>. Returns the number added, or null on a concurrent status change.
    /// </summary>
    /// <remarks>
    /// <paramref name="requirementSeeds"/> (keyed by traveler) are inserted for the new applicants in the same transaction.
    /// When <paramref name="revertVerifiedToPending"/> is set, a DocumentsVerified case returns to DocumentsPending.
    /// </remarks>
    Task<int?> AddApplicantsAsync(int tenantId, int caseId, string expectedStatus, IReadOnlyList<int> travelerIds, int? userId,
        IReadOnlyList<Features.TravelNexus.Documents.DTOs.TravelRequirementSeed> requirementSeeds, bool revertVerifiedToPending,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveApplicantAsync(int tenantId, int caseId, int applicantId, string expectedStatus, int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an applicant decision and, when every active applicant is decided, sets the derived case outcome —
    /// all in one transaction. <c>Applied</c> is false when the applicant is missing or the case status changed.
    /// </summary>
    Task<TravelVisaDecisionResult> RecordDecisionAsync(int tenantId, int caseId, int applicantId, string expectedCaseStatus,
        RecordTravelVisaDecisionDto decision, int? userId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
