using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IFleetTripModeRepository
{
    Task<FleetTripModeSettingsDto> GetSettingsAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<FleetTripModeSettingsDto> UpsertSettingsAsync(
        int tenantId,
        UpsertFleetTripModeSettingsRequest body,
        CancellationToken cancellationToken = default);

    Task<FleetTripCreationMode> GetModeForVehicleAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<(int TenantId, string? TimeZoneId)> GetVehicleTenantContextAsync(
        int vehicleId,
        CancellationToken cancellationToken = default);
}
