using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IFleetAnomalyEvaluator
{
    Task EvaluateAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        decimal? previousSpeedKmh,
        DateTime timestampUtc,
        CancellationToken cancellationToken = default);
}
