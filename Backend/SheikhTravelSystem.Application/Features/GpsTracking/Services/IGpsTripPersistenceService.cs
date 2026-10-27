using System.Data;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGpsTripPersistenceService
{
    Task TryPersistRecentTripsAsync(
        IDbConnection connection,
        int vehicleId,
        CancellationToken cancellationToken);

    /// <summary>Catch-up daily auto-trips for all tenants in AutoGpsDaily mode (hosted job).</summary>
    Task SyncAutoDailyTripsAsync(CancellationToken cancellationToken);
}
