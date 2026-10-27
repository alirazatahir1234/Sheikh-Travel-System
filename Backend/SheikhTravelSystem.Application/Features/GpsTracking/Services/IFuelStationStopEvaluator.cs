using System.Data;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Raises <c>fuel_station_stop</c> when a vehicle remains stopped near a petrol-station POI.
/// </summary>
public interface IFuelStationStopEvaluator
{
    Task EvaluateAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken = default);
}
