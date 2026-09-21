namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGpsTraccarFleetFetcher
{
    Task<Dictionary<int, int>> ResolveVehicleToDeviceMapAsync(
        int tenantId, IEnumerable<int> vehicleIds, CancellationToken cancellationToken);

    Task<bool> HasNonTraccarVehicleAsync(
        int tenantId, IEnumerable<int> vehicleIds, CancellationToken cancellationToken);
}
