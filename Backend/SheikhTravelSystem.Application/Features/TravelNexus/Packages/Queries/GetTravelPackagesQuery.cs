using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Packages.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Packages.Queries;

public record GetTravelPackagesQuery(string? Status = null, string? PackageType = null) : IRequest<ApiResponse<IReadOnlyList<TravelPackageListItemDto>>>;

public class GetTravelPackagesQueryHandler(ITravelPackageRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelPackagesQuery, ApiResponse<IReadOnlyList<TravelPackageListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelPackageListItemDto>>> Handle(GetTravelPackagesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelPackageListItemDto>>.SuccessResponse(await repository.GetListAsync(tenantContext.GetRequiredTenantId(),
            TravelValues.IsOneOf(request.Status, TravelValues.PackageStatuses) ? request.Status : null,
            TravelValues.IsOneOf(request.PackageType, TravelValues.PackageTypes) ? request.PackageType : null, cancellationToken));
}

public record GetTravelPackageByIdQuery(int Id) : IRequest<ApiResponse<TravelPackageDto>>;

public class GetTravelPackageByIdQueryHandler(ITravelPackageRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelPackageByIdQuery, ApiResponse<TravelPackageDto>>
{
    public async Task<ApiResponse<TravelPackageDto>> Handle(GetTravelPackageByIdQuery request, CancellationToken cancellationToken)
        => ApiResponse<TravelPackageDto>.SuccessResponse(
            await repository.GetByIdAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken)
            ?? throw new NotFoundException("TravelPackage", request.Id));
}
