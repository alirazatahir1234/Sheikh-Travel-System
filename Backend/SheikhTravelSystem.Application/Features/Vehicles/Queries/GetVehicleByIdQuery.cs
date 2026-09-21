using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record GetVehicleByIdQuery(int Id) : IRequest<ApiResponse<VehicleDto>>;

public class GetVehicleByIdQueryHandler(
    IVehicleRepository vehicleRepository,
    ITenantContext tenantContext,
    IFileStorageService fileStorage)
    : IRequestHandler<GetVehicleByIdQuery, ApiResponse<VehicleDto>>
{
    public async Task<ApiResponse<VehicleDto>> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(
            request.Id, tenantContext.GetRequiredTenantId(), cancellationToken);

        if (vehicle is null)
            throw new NotFoundException("Vehicle", request.Id);

        if (!string.IsNullOrWhiteSpace(vehicle.ImageUrl))
            vehicle = vehicle with { ImageUrl = fileStorage.ResolveReadUrl(vehicle.ImageUrl) };

        var lastSeen = GpsUtcDateTime.AsUtc(vehicle.GpsLastSeenAt);
        var lastUpdate = GpsUtcDateTime.AsUtc(vehicle.LocationLastUpdate);
        vehicle = vehicle with
        {
            GpsLastSeenAt = lastSeen,
            LocationLastUpdate = lastUpdate,
            TrackerInstallationDate = GpsUtcDateTime.AsUtc(vehicle.TrackerInstallationDate),
            CreatedAt = GpsUtcDateTime.AsUtc(vehicle.CreatedAt),
            UpdatedAt = GpsUtcDateTime.AsUtc(vehicle.UpdatedAt),
            GpsOnline = IsGpsOnline(lastSeen, lastUpdate)
        };

        return ApiResponse<VehicleDto>.SuccessResponse(vehicle);
    }

    private static bool IsGpsOnline(DateTime? lastSeen, DateTime? lastUpdate)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        return (lastSeen.HasValue && lastSeen.Value > cutoff)
            || (lastUpdate.HasValue && lastUpdate.Value > cutoff);
    }
}
