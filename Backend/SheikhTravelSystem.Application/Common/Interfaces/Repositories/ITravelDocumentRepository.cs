using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Applicant document requirements and file metadata. Every statement is scoped by <c>tenantId</c>; case-level
/// visibility (branch scope) is checked by handlers through the visa case. Writes lock the case in
/// <c>expectedCaseStatus</c> and write history in the same transaction; they return false/null on a concurrent change.
/// </summary>
public interface ITravelDocumentRepository
{
    Task<IReadOnlyList<TravelApplicantDocumentDto>> GetCaseRequirementsAsync(int caseId, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelDocumentFileDto>> GetCaseFilesAsync(int caseId, int tenantId, CancellationToken cancellationToken = default);
    /// <summary>Requirement states of the case's active (non-deleted, non-withdrawn) applicants.</summary>
    Task<IReadOnlyList<TravelRequirementState>> GetRequirementStatesAsync(int caseId, int tenantId, CancellationToken cancellationToken = default);
    Task<TravelRequirementFacts?> GetRequirementAsync(int requirementId, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> RequirementExistsAsync(int tenantId, int applicantId, int documentTypeId, CancellationToken cancellationToken = default);
    Task<bool> FileHashExistsAsync(int tenantId, int requirementId, string sha256, CancellationToken cancellationToken = default);
    Task<TravelDocumentFileFacts?> GetFileAsync(int fileId, int tenantId, CancellationToken cancellationToken = default);

    Task<int?> AddFileAsync(int tenantId, int caseId, int requirementId, string expectedCaseStatus, TravelNewDocumentFile file,
        int? userId, CancellationToken cancellationToken = default);

    Task<int?> RequestAsync(int tenantId, int caseId, string expectedCaseStatus, RequestTravelDocumentDto dto, int? userId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAdHocAsync(int tenantId, int caseId, int requirementId, string expectedCaseStatus, int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>Sets Verified / Rejected / Waived when the requirement is still in one of <paramref name="expectedStatuses"/>.</summary>
    Task<bool> ReviewAsync(int tenantId, int caseId, int requirementId, string expectedCaseStatus, IReadOnlyList<string> expectedStatuses,
        string newStatus, string? note, DateTime? expiryDate, int? userId, CancellationToken cancellationToken = default);

    Task<PagedResult<TravelMissingDocumentsItemDto>> GetMissingAsync(int tenantId, TravelBranchScope scope, int page, int pageSize,
        CancellationToken cancellationToken = default);
}
