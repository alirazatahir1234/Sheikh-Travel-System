using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.Queries;

public record GetTravelSuppliersQuery(string? SupplierType = null, bool? IsActive = null) : IRequest<ApiResponse<IReadOnlyList<TravelSupplierDto>>>;

public class GetTravelSuppliersQueryHandler(ITravelSupplierRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSuppliersQuery, ApiResponse<IReadOnlyList<TravelSupplierDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelSupplierDto>>> Handle(GetTravelSuppliersQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelSupplierDto>>.SuccessResponse(await repository.GetListAsync(tenantContext.GetRequiredTenantId(),
            TravelValues.IsOneOf(request.SupplierType, TravelValues.SupplierTypes) ? request.SupplierType : null, request.IsActive, cancellationToken));
}

/// <summary>Airport lookup (platform reference data; no tenant data).</summary>
public record GetTravelAirportsQuery(string? Search) : IRequest<ApiResponse<IReadOnlyList<TravelAirportDto>>>;

public class GetTravelAirportsQueryHandler(ITravelConfigRepository config) : IRequestHandler<GetTravelAirportsQuery, ApiResponse<IReadOnlyList<TravelAirportDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelAirportDto>>> Handle(GetTravelAirportsQuery request, CancellationToken cancellationToken)
    {
        var search = request.Search?.Trim();
        return ApiResponse<IReadOnlyList<TravelAirportDto>>.SuccessResponse(
            await config.GetAirportsAsync(string.IsNullOrEmpty(search) ? null : search[..Math.Min(search.Length, 50)], null, cancellationToken));
    }
}
