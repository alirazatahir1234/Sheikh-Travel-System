using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.Services;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

/// <summary>
/// Evaluates road-posted speed-limit overspeed on GPS ingest.
/// Tenant gate: Alerts module + <see cref="TenantModuleCatalog.RoadSpeedAlertsModuleCode"/> (premium).
/// Threshold: currentSpeed ≥ roadLimitKmh + <see cref="RoadSpeedAlertOptions.OverspeedMarginKmh"/>.
/// </summary>
public sealed class RoadSpeedAlertEvaluator(
    IAlertModuleGate alertModuleGate,
    IRoadSpeedLimitLookup speedLimitLookup,
    IGpsAlertWriter alertWriter,
    INotificationDecisionEngine decisionEngine,
    IDriverTelematicsEnforcementService telematicsEnforcement,
    IOptions<RoadSpeedAlertOptions> options,
    ILogger<RoadSpeedAlertEvaluator> logger) : IRoadSpeedAlertEvaluator
{
    public const string EventType = "road_speed_exceeded";

    /// <summary>
    /// Last successful limit used for a vehicle (process-local). When movement is below
    /// <see cref="RoadSpeedAlertOptions.MinMovementMeters"/>, reuse this limit — no API / durable read.
    /// </summary>
    private static readonly ConcurrentDictionary<int, VehicleLimitState> LastByVehicle = new();

    public async Task EvaluateAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        double? previousLatitude,
        double? previousLongitude,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.ApiEnabled)
            return;

        if (dto.Speed < opts.MinSpeedToEvaluateKmh)
            return;

        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
            new { Id = dto.VehicleId },
            cancellationToken: cancellationToken));

        if (tenantId is null or <= 0)
            return;

        // Never call Google / raise alerts when Alerts parent or road-speed child is off.
        if (!await alertModuleGate.IsAlertTypeEnabledAsync(
                tenantId.Value, EventType, cancellationToken))
            return;

        RoadSpeedLimitResult? limit = null;

        if (LastByVehicle.TryGetValue(dto.VehicleId, out var last)
            && DistanceMeters(last.Lat, last.Lng, dto.Latitude, dto.Longitude) < opts.MinMovementMeters)
        {
            // Same segment / negligible movement — reuse in-process limit (zero Google cost).
            limit = new RoadSpeedLimitResult(last.SpeedLimitKmh, last.PlaceId, last.CacheKey, FromCache: true);
        }
        else
        {
            limit = await speedLimitLookup.GetSpeedLimitKmhAsync(dto.Latitude, dto.Longitude, cancellationToken);
            if (limit is not null)
            {
                LastByVehicle[dto.VehicleId] = new VehicleLimitState(
                    dto.Latitude, dto.Longitude, limit.SpeedLimitKmh, limit.PlaceId, limit.CacheKey);
            }
            else if (LastByVehicle.TryGetValue(dto.VehicleId, out var stale))
            {
                // API/cache miss — keep comparing against last known limit for this vehicle.
                limit = new RoadSpeedLimitResult(stale.SpeedLimitKmh, stale.PlaceId, stale.CacheKey, FromCache: true);
            }
        }

        if (limit is null)
            return;

        await MaybeRaiseAlertAsync(connection, dto, limit, opts, tenantId.Value, timestamp, cancellationToken);
    }

    private async Task MaybeRaiseAlertAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        RoadSpeedLimitResult limit,
        RoadSpeedAlertOptions opts,
        int tenantId,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var threshold = limit.SpeedLimitKmh + opts.OverspeedMarginKmh;
        if (dto.Speed < threshold)
            return;

        var dedupMinutes = Math.Max(1, opts.AlertDedupMinutes);
        var recent = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = @EventType AND IsDeleted = 0
                  AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
            ) THEN 1 ELSE 0 END
            """,
            new { dto.VehicleId, EventType, Minutes = dedupMinutes, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recent)
            return;

        var message =
            $"Speed {dto.Speed:F0} km/h exceeds road limit {limit.SpeedLimitKmh:F0} km/h by ≥{opts.OverspeedMarginKmh:F0} km/h";

        var alertId = await alertWriter.InsertAsync(
            connection, dto.VehicleId, dto.Latitude, dto.Longitude, dto.Speed,
            EventType, message, timestamp, driverId: dto.DriverId,
            cancellationToken: cancellationToken);
        if (alertId <= 0)
            return;

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            EventType,
            "Road speed alert",
            $"Vehicle #{dto.VehicleId}: {message}.",
            NotificationType.TripDelayed,
            ReferenceId: dto.VehicleId,
            AlertEventId: alertId,
            TenantId: tenantId,
            SuggestedPriority: 3,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser,
                NotificationChannels.Push, NotificationChannels.Sms
            ]), cancellationToken);

        try
        {
            await telematicsEnforcement.ProcessAlertAsync(connection, alertId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telematics enforcement failed for road speed alert {AlertId}", alertId);
        }
    }

    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        static double Rad(double d) => d * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    private sealed record VehicleLimitState(
        double Lat,
        double Lng,
        decimal SpeedLimitKmh,
        string? PlaceId,
        string CacheKey);
}
