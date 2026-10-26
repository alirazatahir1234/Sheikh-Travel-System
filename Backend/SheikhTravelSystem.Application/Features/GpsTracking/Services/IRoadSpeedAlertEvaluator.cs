using System.Data;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Road-posted speed-limit overspeed evaluation hooked into GPS position ingest.
/// Gated by tenant module <c>ROAD_SPEED_ALERTS</c> and <see cref="RoadSpeedAlertOptions"/>.
/// </summary>
public interface IRoadSpeedAlertEvaluator
{
    Task EvaluateAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        double? previousLatitude,
        double? previousLongitude,
        DateTime timestamp,
        CancellationToken cancellationToken = default);
}
