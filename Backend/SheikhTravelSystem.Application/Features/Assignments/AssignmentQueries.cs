using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Assignments;

public record ListAssignmentsQuery(
    int Page = 1, int PageSize = 20, string? Search = null, string? Status = null,
    string? AssignmentType = null, int? VehicleId = null, int? DriverId = null,
    int? BranchId = null, int? DepartmentId = null, DateTime? DateFrom = null, DateTime? DateTo = null)
    : IRequest<ApiResponse<PagedResult<AssignmentListItemDto>>>;

public record GetAssignmentStatsQuery : IRequest<ApiResponse<AssignmentStatsDto>>;

public record GetAssignmentChangelogQuery(int AssignmentId) : IRequest<ApiResponse<IReadOnlyList<AssignmentChangelogDto>>>;

public record ValidateAssignmentQuery(ValidateAssignmentRequest Body) : IRequest<ApiResponse<AssignmentValidationResultDto>>;

public record GetAssignmentCalendarQuery(DateTime From, DateTime To, string View = "vehicles", int? BranchId = null)
    : IRequest<ApiResponse<IReadOnlyList<AssignmentCalendarItemDto>>>;

public record GetAssignmentUtilizationReportQuery : IRequest<ApiResponse<AssignmentUtilizationReportDto>>;

public class ListAssignmentsQueryHandler(IAssignmentRepository assignmentRepository, ITenantContext tenantContext)
    : IRequestHandler<ListAssignmentsQuery, ApiResponse<PagedResult<AssignmentListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<AssignmentListItemDto>>> Handle(ListAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var result = await assignmentRepository.ListAsync(
            tenantContext.GetRequiredTenantId(), request.Page, request.PageSize, request.Search, request.Status,
            request.AssignmentType, request.VehicleId, request.DriverId, request.BranchId, request.DepartmentId,
            request.DateFrom, request.DateTo, cancellationToken);
        return ApiResponse<PagedResult<AssignmentListItemDto>>.SuccessResponse(result);
    }
}

public class GetAssignmentStatsQueryHandler(IAssignmentRepository assignmentRepository, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentStatsQuery, ApiResponse<AssignmentStatsDto>>
{
    public async Task<ApiResponse<AssignmentStatsDto>> Handle(GetAssignmentStatsQuery request, CancellationToken cancellationToken)
    {
        var dto = await assignmentRepository.GetStatsAsync(tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<AssignmentStatsDto>.SuccessResponse(dto);
    }
}

public class ValidateAssignmentQueryHandler(IAssignmentRepository assignmentRepository, ITenantContext tenantContext)
    : IRequestHandler<ValidateAssignmentQuery, ApiResponse<AssignmentValidationResultDto>>
{
    public async Task<ApiResponse<AssignmentValidationResultDto>> Handle(ValidateAssignmentQuery request, CancellationToken cancellationToken)
    {
        var result = await assignmentRepository.ValidateAsync(
            tenantContext.GetRequiredTenantId(), request.Body, cancellationToken: cancellationToken);
        return ApiResponse<AssignmentValidationResultDto>.SuccessResponse(result);
    }
}

public class GetAssignmentChangelogQueryHandler(IAssignmentRepository assignmentRepository, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentChangelogQuery, ApiResponse<IReadOnlyList<AssignmentChangelogDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AssignmentChangelogDto>>> Handle(
        GetAssignmentChangelogQuery request, CancellationToken cancellationToken)
    {
        var rows = await assignmentRepository.GetChangelogAsync(
            tenantContext.GetRequiredTenantId(), request.AssignmentId, cancellationToken);
        return ApiResponse<IReadOnlyList<AssignmentChangelogDto>>.SuccessResponse(rows);
    }
}

public class GetAssignmentCalendarQueryHandler(IAssignmentRepository assignmentRepository, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentCalendarQuery, ApiResponse<IReadOnlyList<AssignmentCalendarItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AssignmentCalendarItemDto>>> Handle(
        GetAssignmentCalendarQuery request, CancellationToken cancellationToken)
    {
        var rows = await assignmentRepository.GetCalendarAsync(
            tenantContext.GetRequiredTenantId(), request.From, request.To, request.BranchId, cancellationToken);
        return ApiResponse<IReadOnlyList<AssignmentCalendarItemDto>>.SuccessResponse(rows);
    }
}

public class GetAssignmentUtilizationReportQueryHandler(IAssignmentRepository assignmentRepository, ITenantContext tenantContext)
    : IRequestHandler<GetAssignmentUtilizationReportQuery, ApiResponse<AssignmentUtilizationReportDto>>
{
    public async Task<ApiResponse<AssignmentUtilizationReportDto>> Handle(
        GetAssignmentUtilizationReportQuery request, CancellationToken cancellationToken)
    {
        var dto = await assignmentRepository.GetUtilizationReportAsync(
            tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<AssignmentUtilizationReportDto>.SuccessResponse(dto);
    }
}
