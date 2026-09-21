using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record GetVehiclesQuery(int Page = 1, int PageSize = 20, bool IncludeDrafts = false)
    : IRequest<ApiResponse<PagedResult<VehicleListItemDto>>>;

public class GetVehiclesQueryHandler(
    IVehicleRepository vehicleRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine,
    IFileStorageService fileStorage)
    : IRequestHandler<GetVehiclesQuery, ApiResponse<PagedResult<VehicleListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<VehicleListItemDto>>> Handle(GetVehiclesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        DataScopeResult? scope = null;
        if (currentUser.UserId is int userId)
            scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        var (items, totalCount) = await vehicleRepository.GetPagedAsync(
            tenantId, request.Page, request.PageSize, request.IncludeDrafts, scope, cancellationToken);

        var vehicles = items
            .Select(v => string.IsNullOrWhiteSpace(v.ImageUrl)
                ? v
                : v with { ImageUrl = fileStorage.ResolveReadUrl(v.ImageUrl) })
            .ToList();

        return ApiResponse<PagedResult<VehicleListItemDto>>.SuccessResponse(new PagedResult<VehicleListItemDto>
        {
            Items = vehicles,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
