using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverByIdQuery(int Id) : IRequest<ApiResponse<DriverDto>>;

public class GetDriverByIdQueryHandler(
    IDriverRepository driverRepository,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetDriverByIdQuery, ApiResponse<DriverDto>>
{
    public async Task<ApiResponse<DriverDto>> Handle(GetDriverByIdQuery request, CancellationToken cancellationToken)
    {
        var driver = await driverRepository.GetByIdAsync(
            tenantContext.GetRequiredTenantId(), request.Id, cancellationToken);

        if (!string.IsNullOrWhiteSpace(driver.PhotoUrl))
            driver = driver with { PhotoUrl = fileStorage.ResolveReadUrl(driver.PhotoUrl) };

        return ApiResponse<DriverDto>.SuccessResponse(driver);
    }
}
