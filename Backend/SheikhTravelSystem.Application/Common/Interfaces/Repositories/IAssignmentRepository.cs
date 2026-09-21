using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Assignments;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for assignments. SQL lives in Infrastructure.
/// </summary>
public interface IAssignmentRepository
{
    Task<AssignmentValidationResultDto> ValidateAsync(
        int tenantId,
        ValidateAssignmentRequest request,
        int? excludeAssignmentId = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AssignmentListItemDto>> ListAsync(
        int tenantId,
        int page,
        int pageSize,
        string? search,
        string? status,
        string? assignmentType,
        int? vehicleId,
        int? driverId,
        int? branchId,
        int? departmentId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<AssignmentStatsDto> GetStatsAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentChangelogDto>> GetChangelogAsync(
        int tenantId, int assignmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssignmentCalendarItemDto>> GetCalendarAsync(
        int tenantId, DateTime from, DateTime to, int? branchId,
        CancellationToken cancellationToken = default);

    Task<AssignmentUtilizationReportDto> GetUtilizationReportAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        int tenantId, CreateAssignmentRequest body, string createdBy,
        CancellationToken cancellationToken = default);

    Task<int> TransferAsync(
        int tenantId, int assignmentId, TransferAssignmentRequest body, string modifiedBy,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        int tenantId, int assignmentId, CompleteAssignmentRequest body, string? modifiedBy,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        int tenantId, int assignmentId, CancelAssignmentRequest body, string modifiedBy,
        CancellationToken cancellationToken = default);

    Task ApproveAsync(
        int tenantId, int assignmentId, string? notes, string approvedBy,
        CancellationToken cancellationToken = default);

    Task RejectAsync(
        int tenantId, int assignmentId, string reason, string modifiedBy,
        CancellationToken cancellationToken = default);
}
