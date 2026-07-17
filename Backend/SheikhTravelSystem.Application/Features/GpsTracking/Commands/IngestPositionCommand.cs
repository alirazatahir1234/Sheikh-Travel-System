using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record IngestPositionCommand(IngestPositionDto Position) : IRequest<ApiResponse<bool>>;

public class IngestPositionCommandValidator : AbstractValidator<IngestPositionCommand>
{
    public IngestPositionCommandValidator()
    {
        RuleFor(x => x.Position.VehicleId).GreaterThan(0);
        RuleFor(x => x.Position.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Position.Longitude).InclusiveBetween(-180, 180);
    }
}

public class IngestPositionCommandHandler(
    IDbConnectionFactory dbFactory,
    INotificationService notifications,
    ILocationBroadcastService broadcaster,
    ICurrentUserService currentUser,
    IOptions<TraccarOptions> traccarOptions,
    IOptions<GpsSettings> gpsSettings,
    IGpsAddressBackfillQueue addressBackfillQueue)
    : IRequestHandler<IngestPositionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(IngestPositionCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Position;

        if (currentUser.Role == "Driver")
        {
            var driverId = currentUser.DriverId;
            if (!driverId.HasValue)
                return ApiResponse<bool>.FailResponse("Driver identity required.");

            var allowed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Bookings
                    WHERE DriverId = @DriverId AND VehicleId = @VehicleId AND Status = @Started AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { DriverId = driverId.Value, dto.VehicleId, Started = (int)BookingStatus.Started },
                cancellationToken: cancellationToken));

            if (!allowed)
                return ApiResponse<bool>.FailResponse("GPS ingest only allowed during your active started trip for this vehicle.");

            dto = dto with { DriverId = driverId.Value };
        }

        var recordedAt = DateTime.UtcNow;

        // Read the previous state before ingestion overwrites the row — both previousSpeed (for
        // trip-persistence detection below) and previousIgnition (for the ignition-transition
        // alert) depend on this happening first; a future reordering of these calls would
        // silently break both.
        var previous = await connection.QueryFirstOrDefaultAsync<(decimal? Speed, bool? Ignition)>(new CommandDefinition(
            "SELECT Speed, Ignition FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
            new { dto.VehicleId },
            cancellationToken: cancellationToken));
        var previousSpeed = previous.Speed;
        var previousIgnition = previous.Ignition;

        await GpsPositionIngestionHelper.IngestAsync(connection, dto, recordedAt, cancellationToken);

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            addressBackfillQueue.Enqueue(dto.VehicleId, dto.Latitude, dto.Longitude);
        }

        var bookingId = await GpsPositionIngestionHelper.ResolveActiveBookingIdAsync(
            connection, dto.VehicleId, dto.BookingId, cancellationToken);

        if (dto.GpsDeviceId.HasValue)
        {
            await GpsDeviceTelemetryUpdater.UpdateAsync(
                connection,
                dto.GpsDeviceId.Value,
                recordedAt,
                dto.Ignition,
                dto.Speed,
                cancellationToken: cancellationToken);
        }

        var ingestDto = dto with { BookingId = bookingId };
        await EvaluateAlertsAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluateSosAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluateIgnitionTransitionAsync(connection, ingestDto, previousIgnition, recordedAt, cancellationToken);
        await EvaluateLowFuelAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluateLowBatteryAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluatePowerCutAsync(connection, ingestDto, recordedAt, cancellationToken);
        await EvaluateGpsLostAsync(connection, ingestDto, recordedAt, cancellationToken);

        // A position just arrived for this vehicle, so it's no longer offline — clear any
        // outstanding offline alert the background detector raised while it was unreachable, and
        // fire a one-time "online" event (rows affected > 0 means it actually was flagged offline).
        var clearedOffline = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GpsAlertEvents
              SET IsAcknowledged = 1, Status = 'acknowledged', AcknowledgedAt = GETUTCDATE(), AcknowledgedBy = 'system'
              WHERE VehicleId = @VehicleId AND EventType = 'vehicle_offline' AND IsAcknowledged = 0 AND IsDeleted = 0",
            new { dto.VehicleId },
            cancellationToken: cancellationToken));

        if (clearedOffline > 0)
        {
            await InsertAlertAsync(connection, null, ingestDto, null, "online",
                "Vehicle back online", recordedAt, cancellationToken);
        }

        if (GpsPositionIngestionHelper.ShouldAttemptTripPersistence(dto.Speed, dto.Ignition, previousSpeed))
        {
            await GpsTripPersistenceService.TryPersistRecentTripsAsync(connection, dto.VehicleId, cancellationToken);
        }

        await broadcaster.BroadcastLocationUpdateAsync(
            dto.VehicleId,
            bookingId,
            dto.Latitude,
            dto.Longitude,
            dto.Speed,
            dto.Ignition,
            recordedAt,
            dto.Heading,
            dto.FuelLevel,
            dto.BatteryLevel,
            dto.GsmSignal,
            dto.TotalDistanceKm,
            dto.Address,
            dto.AlarmType,
            dto.Temperature,
            cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Position recorded.");
    }

    private async Task EvaluateSosAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.AlarmType))
        {
            return;
        }

        var sosValues = traccarOptions.Value.SosAlarmValues;
        var isSos = sosValues is { Length: > 0 } &&
            sosValues.Any(v => string.Equals(v, dto.AlarmType, StringComparison.OrdinalIgnoreCase));

        if (!isSos)
        {
            return;
        }

        // Suppress re-alerting for the same ongoing incident within a short window.
        var recentUnacknowledged = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = 'sos' AND IsAcknowledged = 0 AND IsDeleted = 0
                AND Timestamp > DATEADD(SECOND, -60, @Timestamp)
              ) THEN 1 ELSE 0 END",
            new { dto.VehicleId, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recentUnacknowledged)
        {
            return;
        }

        await InsertAlertAsync(connection, null, dto, null, "sos",
            "SOS / panic alarm triggered", timestamp, cancellationToken);

        await notifications.CreateForAllAsync(
            "SOS alert",
            $"Vehicle #{dto.VehicleId} triggered an SOS/panic alarm.",
            NotificationType.Sos,
            dto.VehicleId,
            cancellationToken);

        await broadcaster.BroadcastSosAlertAsync(dto.VehicleId, dto.Latitude, dto.Longitude, timestamp, cancellationToken);
    }

    private async Task EvaluateAlertsAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var rules = await connection.QueryAsync<(int Id, int? VehicleId, decimal? SpeedLimitKmh, int? GeofenceId, bool AlertOnEnter, bool AlertOnExit)>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, SpeedLimitKmh, GeofenceId, AlertOnEnter, AlertOnExit
                  FROM GpsAlertRules WHERE IsActive = 1 AND IsDeleted = 0
                  AND (VehicleId IS NULL OR VehicleId = @VehicleId)",
                new { dto.VehicleId },
                cancellationToken: cancellationToken));

        foreach (var rule in rules)
        {
            if (rule.SpeedLimitKmh.HasValue && dto.Speed > rule.SpeedLimitKmh.Value)
            {
                // Re-alert at most every OverspeedDedupMinutes while continuously over the limit,
                // rather than once per position tick.
                var recentOverspeed = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM GpsAlertEvents
                        WHERE VehicleId = @VehicleId AND EventType = 'speed_exceeded' AND IsDeleted = 0
                        AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
                      ) THEN 1 ELSE 0 END",
                    new { dto.VehicleId, Minutes = OverspeedDedupMinutes, Timestamp = timestamp },
                    cancellationToken: cancellationToken));

                if (recentOverspeed)
                {
                    continue;
                }

                await InsertAlertAsync(connection, rule.Id, dto, null, "speed_exceeded",
                    $"Speed {dto.Speed:F0} km/h exceeds limit {rule.SpeedLimitKmh:F0} km/h",
                    timestamp, cancellationToken);
                await notifications.CreateForAllAsync(
                    "Speed alert",
                    $"Vehicle #{dto.VehicleId} exceeded {rule.SpeedLimitKmh:F0} km/h (current {dto.Speed:F0} km/h).",
                    NotificationType.TripDelayed,
                    dto.VehicleId,
                    cancellationToken);
            }
        }

        await EvaluateGeofenceCrossingsAsync(connection, dto, rules.ToList(), timestamp, cancellationToken);
    }

    private const int OverspeedDedupMinutes = 5;

    private static async Task EvaluateGeofenceCrossingsAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        IReadOnlyList<(int Id, int? VehicleId, decimal? SpeedLimitKmh, int? GeofenceId, bool AlertOnEnter, bool AlertOnExit)> rules,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var vehicle = await connection.QueryFirstOrDefaultAsync<(int? BranchId, int? DepartmentId)>(
            new CommandDefinition(
                "SELECT BranchId, DepartmentId FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = dto.VehicleId },
                cancellationToken: cancellationToken));

        var assigned = (await connection.QueryAsync<(int Id, string Name, string AreaType, double CenterLat, double CenterLng, double RadiusMeters, string? GeoJson)>(
            new CommandDefinition(
                """
                SELECT DISTINCT g.Id, g.Name, g.AreaType, g.CenterLat, g.CenterLng, g.RadiusMeters, g.GeoJson
                FROM Geofences g
                INNER JOIN GeofenceAssignments a ON a.GeofenceId = g.Id AND a.IsDeleted = 0
                WHERE g.IsActive = 1 AND g.IsDeleted = 0
                  AND (
                    a.VehicleId = @VehicleId
                    OR (@BranchId IS NOT NULL AND a.BranchId = @BranchId)
                    OR (@DepartmentId IS NOT NULL AND a.DepartmentId = @DepartmentId)
                  )
                """,
                new { dto.VehicleId, vehicle.BranchId, vehicle.DepartmentId },
                cancellationToken: cancellationToken))).ToList();

        // Flags: AlertOnEnter / AlertOnExit. Assignments always use (true, true).
        var watch = new Dictionary<int, (string Name, string AreaType, double CenterLat, double CenterLng, double RadiusMeters, string? GeoJson, bool OnEnter, bool OnExit, int? RuleId)>();

        foreach (var g in assigned)
        {
            watch[g.Id] = (g.Name, g.AreaType, g.CenterLat, g.CenterLng, g.RadiusMeters, g.GeoJson, true, true, null);
        }

        foreach (var rule in rules.Where(r => r.GeofenceId.HasValue))
        {
            var gid = rule.GeofenceId!.Value;
            if (watch.ContainsKey(gid))
            {
                var existing = watch[gid];
                watch[gid] = existing with
                {
                    OnEnter = existing.OnEnter || rule.AlertOnEnter,
                    OnExit = existing.OnExit || rule.AlertOnExit,
                    RuleId = rule.Id
                };
                continue;
            }

            var geofence = await connection.QueryFirstOrDefaultAsync<(int Id, string Name, string AreaType, double CenterLat, double CenterLng, double RadiusMeters, string? GeoJson)>(
                new CommandDefinition(
                    @"SELECT Id, Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson
                      FROM Geofences WHERE Id = @Id AND IsActive = 1 AND IsDeleted = 0",
                    new { Id = gid },
                    cancellationToken: cancellationToken));

            if (geofence.Id == 0) continue;

            watch[gid] = (geofence.Name, geofence.AreaType, geofence.CenterLat, geofence.CenterLng,
                geofence.RadiusMeters, geofence.GeoJson, rule.AlertOnEnter, rule.AlertOnExit, rule.Id);
        }

        foreach (var (geofenceId, g) in watch)
        {
            var inside = GpsGeoHelper.IsInsideGeofence(
                dto.Latitude, dto.Longitude, g.AreaType, g.CenterLat, g.CenterLng, g.RadiusMeters, g.GeoJson);

            var lastEvent = await connection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
                @"SELECT TOP 1 EventType FROM GpsAlertEvents
                  WHERE VehicleId = @VehicleId AND GeofenceId = @GeofenceId AND IsDeleted = 0
                    AND EventType IN ('geofence_enter', 'geofence_exit')
                  ORDER BY Timestamp DESC",
                new { dto.VehicleId, GeofenceId = geofenceId },
                cancellationToken: cancellationToken));

            var wasInside = lastEvent == "geofence_enter";

            if (inside && !wasInside && g.OnEnter)
            {
                await InsertAlertAsync(connection, g.RuleId, dto, geofenceId, "geofence_enter",
                    $"Entered geofence: {g.Name}", timestamp, cancellationToken);
            }
            else if (!inside && wasInside && g.OnExit)
            {
                await InsertAlertAsync(connection, g.RuleId, dto, geofenceId, "geofence_exit",
                    $"Exited geofence: {g.Name}", timestamp, cancellationToken);
            }
        }
    }

    private static async Task EvaluateIgnitionTransitionAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        bool? previousIgnition,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        if (!dto.Ignition.HasValue || !previousIgnition.HasValue || dto.Ignition == previousIgnition)
        {
            return;
        }

        var eventType = dto.Ignition.Value ? "ignition_on" : "ignition_off";
        var message = dto.Ignition.Value ? "Ignition turned ON" : "Ignition turned OFF";

        await InsertAlertAsync(connection, null, dto, null, eventType, message, timestamp, cancellationToken);
    }

    private const decimal LowFuelThresholdPercent = 15m;

    private static async Task EvaluateLowFuelAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        if (!dto.FuelLevel.HasValue || dto.FuelLevel.Value >= LowFuelThresholdPercent)
        {
            return;
        }

        // Re-alert at most once/hour while fuel stays low, rather than once per position tick —
        // fuel level changes slowly, unlike SOS's 60-second window for an active incident.
        var recentAlert = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = 'low_fuel' AND IsDeleted = 0
                AND Timestamp > DATEADD(HOUR, -1, @Timestamp)
              ) THEN 1 ELSE 0 END",
            new { dto.VehicleId, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recentAlert)
        {
            return;
        }

        await InsertAlertAsync(connection, null, dto, null, "low_fuel",
            $"Low fuel — {dto.FuelLevel.Value:F0}%", timestamp, cancellationToken);
    }

    private async Task EvaluateLowBatteryAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var threshold = gpsSettings.Value.LowBatteryThresholdPercent;
        if (!dto.BatteryLevel.HasValue || dto.BatteryLevel.Value >= threshold)
        {
            return;
        }

        // 1h dedup — mirrors EvaluateLowFuelAsync, since battery level also changes slowly.
        var recentAlert = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = 'low_battery' AND IsDeleted = 0
                AND Timestamp > DATEADD(HOUR, -1, @Timestamp)
              ) THEN 1 ELSE 0 END",
            new { dto.VehicleId, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recentAlert)
        {
            return;
        }

        await InsertAlertAsync(connection, null, dto, null, "low_battery",
            $"Low device battery — {dto.BatteryLevel.Value:F0}%", timestamp, cancellationToken);
    }

    private static readonly string[] PowerCutAlarmValues = ["powercut", "poweroff", "powerdisconnect"];
    private const int PowerCutDedupMinutes = 5;

    private static async Task EvaluatePowerCutAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.AlarmType) ||
            !PowerCutAlarmValues.Contains(dto.AlarmType.ToLowerInvariant()))
        {
            return;
        }

        var recentAlert = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = 'power_cut' AND IsDeleted = 0
                AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
              ) THEN 1 ELSE 0 END",
            new { dto.VehicleId, Minutes = PowerCutDedupMinutes, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recentAlert)
        {
            return;
        }

        await InsertAlertAsync(connection, null, dto, null, "power_cut",
            "External power disconnected", timestamp, cancellationToken);
    }

    private static readonly string[] GpsLostAlarmValues = ["gpsantennacut", "gpslost", "nofix", "gpsjamming"];
    private const int GpsLostDedupMinutes = 5;

    private static async Task EvaluateGpsLostAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.AlarmType) ||
            !GpsLostAlarmValues.Contains(dto.AlarmType.ToLowerInvariant()))
        {
            return;
        }

        var recentAlert = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = 'gps_lost' AND IsDeleted = 0
                AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
              ) THEN 1 ELSE 0 END",
            new { dto.VehicleId, Minutes = GpsLostDedupMinutes, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recentAlert)
        {
            return;
        }

        await InsertAlertAsync(connection, null, dto, null, "gps_lost",
            "GPS signal lost", timestamp, cancellationToken);
    }

    private static Task InsertAlertAsync(
        System.Data.IDbConnection connection,
        int? ruleId,
        IngestPositionDto dto,
        int? geofenceId,
        string eventType,
        string message,
        DateTime timestamp,
        CancellationToken cancellationToken)
        => GpsAlertWriter.InsertAsync(
            connection, dto.VehicleId, dto.Latitude, dto.Longitude, dto.Speed,
            eventType, message, timestamp, ruleId, geofenceId, dto.DriverId,
            cancellationToken: cancellationToken);
}
