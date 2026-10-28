using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
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
    string? Availability = null,
    int? TenantId = null) : IRequest<ApiResponse<PagedResult<DriverListItemDto>>>;

public class GetDriversQueryHandler(
    IDriverRepository driverRepository,
    IPlatformScope platformScope,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetDriversQuery, ApiResponse<PagedResult<DriverListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverListItemDto>>> Handle(GetDriversQuery request, CancellationToken cancellationToken)
    {
        int tenantId;
        try
        {
            tenantId = ResolveTenantFilter(request.TenantId);
        }
        catch (InvalidOperationException)
        {
            throw new ForbiddenException(
                "Company context is missing. Sign in again or select a company (tenant) before loading drivers.");
        }

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

    private int ResolveTenantFilter(int? requestedTenantId)
    {
        if (requestedTenantId.HasValue)
        {
            platformScope.EnsureTenantAccess(requestedTenantId.Value);
            return requestedTenantId.Value;
        }

        return platformScope.TenantId;
    }
}

public record GetDriverStatsQuery(int? BranchId = null, int? TenantId = null) : IRequest<ApiResponse<DriverStatsDto>>;

public class GetDriverStatsQueryHandler(
    IDriverRepository driverRepository,
    IPlatformScope platformScope,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine,
    IAppCache cache)
    : IRequestHandler<GetDriverStatsQuery, ApiResponse<DriverStatsDto>>
{
    public async Task<ApiResponse<DriverStatsDto>> Handle(GetDriverStatsQuery request, CancellationToken cancellationToken)
    {
        if (request.TenantId.HasValue)
            platformScope.EnsureTenantAccess(request.TenantId.Value);
        var tenantId = request.TenantId ?? platformScope.TenantId;
        DataScopeResult? scope = null;
        if (currentUser.UserId is int userId)
            scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        if (scope is not null &&
            !DataScopeSql.TryIntersectOptional(scope, request.BranchId, null, out _, out _, out var scopeError))
            return ApiResponse<DriverStatsDto>.FailResponse(scopeError ?? "Outside data scope.");

        var scopeKey = scope is null
            ? "noscope"
            : $"{scope.Mode}:{(scope.IsCompanyWide ? "co" : string.Join(',', scope.BranchIds))}:u{scope.UserId}";
        var cacheKey = $"drivers:stats:{tenantId}:b{request.BranchId?.ToString() ?? "all"}:{scopeKey}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            AppCacheTtl.DriverStats,
            async ct =>
            {
                var stats = await driverRepository.GetStatsAsync(tenantId, request.BranchId, scope, ct);
                return ApiResponse<DriverStatsDto>.SuccessResponse(stats);
            },
            cancellationToken);
    }
}
