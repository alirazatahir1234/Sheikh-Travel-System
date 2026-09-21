using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriversQuery(
    int Page = 1,
    int PageSize = 20,
    string? Q = null,
    DriverStatus? Status = null,
    int? BranchId = null,
    string? LicenseExpiry = null,
    string? VerificationStatus = null,
    string? Availability = null) : IRequest<ApiResponse<PagedResult<DriverListItemDto>>>;

public class GetDriversQueryHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetDriversQuery, ApiResponse<PagedResult<DriverListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverListItemDto>>> Handle(GetDriversQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        DataScopeResult? scope = null;
        if (currentUser.UserId is int userId)
            scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        var result = await driverRepository.GetPagedAsync(
            tenantId, request.Page, request.PageSize, request.Q, request.Status, request.BranchId,
            request.LicenseExpiry, request.VerificationStatus, request.Availability, scope, cancellationToken);

        if (result.IsScopeFailure)
            return ApiResponse<PagedResult<DriverListItemDto>>.FailResponse(result.ScopeError ?? "Outside data scope.");

        return ApiResponse<PagedResult<DriverListItemDto>>.SuccessResponse(new PagedResult<DriverListItemDto>
        {
            Items = result.Items.ToList(),
            TotalCount = result.TotalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public record GetDriverStatsQuery : IRequest<ApiResponse<DriverStatsDto>>;

public class GetDriverStatsQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverStatsQuery, ApiResponse<DriverStatsDto>>
{
    public async Task<ApiResponse<DriverStatsDto>> Handle(GetDriverStatsQuery request, CancellationToken cancellationToken)
    {
        var stats = await driverRepository.GetStatsAsync(tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<DriverStatsDto>.SuccessResponse(stats);
    }
}
