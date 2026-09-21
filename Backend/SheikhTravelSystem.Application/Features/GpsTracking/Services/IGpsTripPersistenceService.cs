using System.Data;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGpsTripPersistenceService
{
    Task TryPersistRecentTripsAsync(
        IDbConnection connection,
        int vehicleId,
        CancellationToken cancellationToken);
}
