using System.Data;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

public interface IGpsPositionIngestionHelper
{
    Task<int?> ResolveActiveBookingIdAsync(
        IDbConnection connection,
        int vehicleId,
        int? explicitBookingId,
        CancellationToken cancellationToken);

    Task IngestAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        DateTime recordedAt,
        CancellationToken cancellationToken);
}

/// <summary>Pure trip-persistence gate — no SQL.</summary>
public static class GpsPositionIngestionRules
{
    private const decimal MovingSpeedKmh = 10m;

    public static bool ShouldAttemptTripPersistence(decimal speed, bool? ignition, decimal? previousSpeed)
    {
        var stopped = ignition == false || speed <= MovingSpeedKmh;
        var wasMoving = previousSpeed is > MovingSpeedKmh;
        return stopped && wasMoving;
    }
}
