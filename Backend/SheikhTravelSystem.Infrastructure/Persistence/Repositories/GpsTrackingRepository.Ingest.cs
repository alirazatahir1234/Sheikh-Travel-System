using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Infrastructure.Services.Gps;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class GpsTrackingRepository
{
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

        var alertId = await InsertAlertAsync(connection, null, dto, null, "sos",
            "SOS / panic alarm triggered", timestamp, cancellationToken);
        if (alertId <= 0)
            return;

        var sosTenantId = await ResolveVehicleTenantIdAsync(connection, dto.VehicleId, cancellationToken);
        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "sos",
            "SOS alert",
            $"Vehicle #{dto.VehicleId} triggered an SOS/panic alarm.",
            NotificationType.Sos,
            ReferenceId: dto.VehicleId,
            AlertEventId: alertId,
            TenantId: sosTenantId,
            SuggestedPriority: 4,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser,
                NotificationChannels.Push, NotificationChannels.Sms
            ],
            RequireEscalation: true), cancellationToken);

        await escalation.StartAsync("sos", dto.VehicleId, alertEventId: alertId, cancellationToken: cancellationToken);

        await broadcaster.BroadcastSosAlertAsync(dto.VehicleId, dto.Latitude, dto.Longitude, timestamp, cancellationToken);
    }

    private async Task EvaluateAlertsAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var tenantId = await ResolveVehicleTenantIdAsync(connection, dto.VehicleId, cancellationToken);
        if (tenantId is null or <= 0)
            return;

        var rules = await connection.QueryAsync<(int Id, int? VehicleId, decimal? SpeedLimitKmh, int? GeofenceId, bool AlertOnEnter, bool AlertOnExit)>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, SpeedLimitKmh, GeofenceId, AlertOnEnter, AlertOnExit
                  FROM GpsAlertRules
                  WHERE IsActive = 1 AND IsDeleted = 0
                    AND TenantId = @TenantId
                    AND (VehicleId IS NULL OR VehicleId = @VehicleId)",
                new { dto.VehicleId, TenantId = tenantId },
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

                var alertId = await InsertAlertAsync(connection, rule.Id, dto, null, "speed_exceeded",
                    $"Speed {dto.Speed:F0} km/h exceeds limit {rule.SpeedLimitKmh:F0} km/h",
                    timestamp, cancellationToken);
                if (alertId <= 0)
                    continue;

                var speedTenantId = await ResolveVehicleTenantIdAsync(connection, dto.VehicleId, cancellationToken);
                await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                    "speed_exceeded",
                    "Speed alert",
                    $"Vehicle #{dto.VehicleId} exceeded {rule.SpeedLimitKmh:F0} km/h (current {dto.Speed:F0} km/h).",
                    NotificationType.TripDelayed,
                    ReferenceId: dto.VehicleId,
                    AlertEventId: alertId,
                    TenantId: speedTenantId,
                    SuggestedPriority: 3,
                    RequestedChannels:
                    [
                        NotificationChannels.InApp, NotificationChannels.Browser,
                        NotificationChannels.Push, NotificationChannels.Sms
                    ]), cancellationToken);

                try
                {
                    await _telematicsEnforcement.ProcessAlertAsync(connection, alertId, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Telematics enforcement failed for overspeed alert {AlertId}", alertId);
                }
            }
        }

        await EvaluateGeofenceCrossingsAsync(connection, dto, rules.ToList(), timestamp, cancellationToken);
    }

    private const int OverspeedDedupMinutes = 5;

    private async Task EvaluateGeofenceCrossingsAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        IReadOnlyList<(int Id, int? VehicleId, decimal? SpeedLimitKmh, int? GeofenceId, bool AlertOnEnter, bool AlertOnExit)> rules,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var vehicle = await connection.QueryFirstOrDefaultAsync<(int? TenantId, int? BranchId, int? DepartmentId, bool GeofencingEnabled)>(
            new CommandDefinition(
                """
                SELECT v.TenantId, v.BranchId, v.DepartmentId,
                       CAST(CASE WHEN EXISTS (
                           SELECT 1 FROM TenantFeatures tf
                           WHERE tf.TenantId = v.TenantId AND tf.FeatureKey = @FeatureKey AND tf.IsEnabled = 1
                       ) THEN 1 ELSE 0 END AS BIT) AS GeofencingEnabled
                FROM Vehicles v
                WHERE v.Id = @Id AND v.IsDeleted = 0
                """,
                new { Id = dto.VehicleId, FeatureKey = GpsAddOnFeatureCatalog.Geofencing },
                cancellationToken: cancellationToken));

        if (vehicle.TenantId is null or <= 0 || !vehicle.GeofencingEnabled)
            return;

        var ruleZoneIds = rules
            .Where(r => r.GeofenceId.HasValue)
            .Select(r => r.GeofenceId!.Value)
            .Distinct()
            .ToArray();

        // Watched = assigned (vehicle / branch / department) OR referenced by a rule. State is tracked for every
        // watched zone on every position, independent of which Entry/Exit checkboxes are on.
        var zones = (await connection.QueryAsync<WatchedZoneRow>(
            new CommandDefinition(
                """
                SELECT g.Id, g.Name, g.AreaType, g.CenterLat, g.CenterLng, g.RadiusMeters, g.GeoJson,
                       s.IsInside AS StateIsInside, s.SinceUtc AS StateSinceUtc,
                       s.PendingInside, s.PendingCount, s.PendingSinceUtc
                FROM Geofences g
                LEFT JOIN VehicleGeofenceState s ON s.VehicleId = @VehicleId AND s.GeofenceId = g.Id
                WHERE g.IsActive = 1 AND g.IsDeleted = 0 AND g.TenantId = @TenantId
                  AND (
                    g.Id IN @RuleZoneIds
                    OR EXISTS (
                      SELECT 1 FROM GeofenceAssignments a
                      WHERE a.GeofenceId = g.Id AND a.IsDeleted = 0
                        AND (
                          a.VehicleId = @VehicleId
                          OR (@BranchId IS NOT NULL AND a.BranchId = @BranchId)
                          OR (@DepartmentId IS NOT NULL AND a.DepartmentId = @DepartmentId)
                        )
                    )
                  )
                """,
                new
                {
                    dto.VehicleId,
                    vehicle.TenantId,
                    vehicle.BranchId,
                    vehicle.DepartmentId,
                    RuleZoneIds = ruleZoneIds.Length == 0 ? new[] { -1 } : ruleZoneIds
                },
                cancellationToken: cancellationToken))).ToList();

        var evaluatedIds = new List<int>(zones.Count);
        foreach (var zone in zones)
        {
            if (!GpsGeoHelper.TryEvaluateGeofence(
                    dto.Latitude, dto.Longitude, zone.AreaType, zone.CenterLat, zone.CenterLng,
                    zone.RadiusMeters, zone.GeoJson, out var inside, out var boundaryDistance))
            {
                logger.LogDebug("Skipping geofence {GeofenceId}: invalid geometry", zone.Id);
                continue;
            }

            evaluatedIds.Add(zone.Id);

            var previous = zone.StateIsInside.HasValue
                ? new GeofenceZoneState(zone.StateIsInside.Value, zone.StateSinceUtc ?? timestamp,
                    zone.PendingInside, zone.PendingCount ?? 0, zone.PendingSinceUtc)
                : null;

            var result = GeofenceStateEvaluator.Evaluate(previous, inside, boundaryDistance, timestamp);
            if (result.StateChanged)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    MERGE VehicleGeofenceState WITH (HOLDLOCK) AS t
                    USING (SELECT @VehicleId AS VehicleId, @GeofenceId AS GeofenceId) AS src
                      ON t.VehicleId = src.VehicleId AND t.GeofenceId = src.GeofenceId
                    WHEN MATCHED THEN UPDATE SET
                        IsInside = @IsInside, SinceUtc = @SinceUtc, PendingInside = @PendingInside,
                        PendingCount = @PendingCount, PendingSinceUtc = @PendingSinceUtc,
                        LastEvaluatedUtc = @Now, LastLatitude = @Lat, LastLongitude = @Lng
                    WHEN NOT MATCHED THEN INSERT
                        (TenantId, VehicleId, GeofenceId, IsInside, SinceUtc, PendingInside, PendingCount,
                         PendingSinceUtc, LastEvaluatedUtc, LastLatitude, LastLongitude)
                      VALUES (@TenantId, @VehicleId, @GeofenceId, @IsInside, @SinceUtc, @PendingInside, @PendingCount,
                         @PendingSinceUtc, @Now, @Lat, @Lng);
                    """,
                    new
                    {
                        vehicle.TenantId,
                        dto.VehicleId,
                        GeofenceId = zone.Id,
                        result.State.IsInside,
                        result.State.SinceUtc,
                        result.State.PendingInside,
                        result.State.PendingCount,
                        result.State.PendingSinceUtc,
                        Now = timestamp,
                        Lat = dto.Latitude,
                        Lng = dto.Longitude
                    },
                    cancellationToken: cancellationToken));
            }

            if (result.Transition == GeofenceTransition.None)
                continue;

            var rule = rules
                .Where(r => r.GeofenceId == zone.Id)
                .OrderByDescending(r => r.VehicleId.HasValue)
                .FirstOrDefault();
            if (rule.Id == 0 || !GeofenceStateEvaluator.ShouldAlert(result.Transition, rule.AlertOnEnter, rule.AlertOnExit))
                continue;

            var eventType = GeofenceStateEvaluator.EventType(result.Transition);
            var lastSame = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
                """
                SELECT MAX(Timestamp) FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND GeofenceId = @GeofenceId AND EventType = @EventType AND IsDeleted = 0
                  AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
                """,
                new
                {
                    dto.VehicleId,
                    GeofenceId = zone.Id,
                    EventType = eventType,
                    Minutes = GeofenceStateEvaluator.AlertDedupMinutes,
                    Timestamp = timestamp
                },
                cancellationToken: cancellationToken));
            if (GeofenceStateEvaluator.IsWithinDedupWindow(lastSame, timestamp))
                continue;

            var verb = result.Transition == GeofenceTransition.Enter ? "Entered" : "Exited";
            var alertId = await InsertAlertAsync(connection, rule.Id, dto, zone.Id, eventType,
                $"{verb} geofence: {zone.Name}", timestamp, cancellationToken);
            if (alertId <= 0)
                continue;

            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                eventType,
                result.Transition == GeofenceTransition.Enter ? "Geofence entered" : "Geofence exited",
                $"Vehicle #{dto.VehicleId} {verb.ToLowerInvariant()} {zone.Name}.",
                NotificationType.TripDelayed,
                ReferenceId: dto.VehicleId,
                AlertEventId: alertId,
                TenantId: vehicle.TenantId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push
                ]), cancellationToken);
        }

        // One round-trip: freshness for unchanged zones + drop state for zones no longer watched.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE VehicleGeofenceState SET LastEvaluatedUtc = @Now, LastLatitude = @Lat, LastLongitude = @Lng
            WHERE VehicleId = @VehicleId AND GeofenceId IN @Ids AND LastEvaluatedUtc < @Now;
            DELETE FROM VehicleGeofenceState WHERE VehicleId = @VehicleId AND GeofenceId NOT IN @Ids;
            """,
            new
            {
                dto.VehicleId,
                Ids = evaluatedIds.Count == 0 ? new[] { -1 } : evaluatedIds.ToArray(),
                Now = timestamp,
                Lat = dto.Latitude,
                Lng = dto.Longitude
            },
            cancellationToken: cancellationToken));
    }

    private sealed class WatchedZoneRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string AreaType { get; init; } = "circle";
        public double CenterLat { get; init; }
        public double CenterLng { get; init; }
        public double RadiusMeters { get; init; }
        public string? GeoJson { get; init; }
        public bool? StateIsInside { get; init; }
        public DateTime? StateSinceUtc { get; init; }
        public bool? PendingInside { get; init; }
        public int? PendingCount { get; init; }
        public DateTime? PendingSinceUtc { get; init; }
    }

    /// <summary>
    /// Position AlarmType (e.g. VG03 G-sensor <c>accident</c>) and Traccar harsh events deduped before alert insert.
    /// </summary>
    private async Task EvaluateHarshBrakingFromPositionAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var canonical = TraccarHarshEventMapper.TryMapCanonical("alarm", dto.AlarmType)
                        ?? TraccarHarshEventMapper.TryMapCanonical(dto.AlarmType, null);
        if (!string.Equals(canonical, TraccarHarshEventMapper.HarshBraking, StringComparison.OrdinalIgnoreCase))
            return;

        var recentAlert = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = @EventType AND IsDeleted = 0
                  AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
              ) THEN 1 ELSE 0 END",
            new
            {
                dto.VehicleId,
                EventType = TraccarHarshEventMapper.HarshBraking,
                Minutes = GpsHarshBrakingAlertDispatch.DedupMinutes,
                Timestamp = timestamp
            },
            cancellationToken: cancellationToken));

        if (recentAlert)
            return;

        var message = TraccarAlertEventMapper.FormatMessage(TraccarHarshEventMapper.HarshBraking);
        var alertId = await InsertAlertAsync(connection, null, dto, null,
            TraccarHarshEventMapper.HarshBraking, message, timestamp, cancellationToken);
        if (alertId <= 0)
            return;

        var tenantId = await ResolveVehicleTenantIdAsync(connection, dto.VehicleId, cancellationToken);
        await GpsHarshBrakingAlertDispatch.DispatchIfAllowedAsync(
            decisionEngine, connection, dto.VehicleId, alertId, dto.Speed, tenantId, cancellationToken);

        try
        {
            await _telematicsEnforcement.ProcessAlertAsync(connection, alertId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telematics enforcement failed for harsh braking alert {AlertId}", alertId);
        }
    }

    /// <summary>
    /// Debounce window for ACC ignition flapping (false ON/OFF from noisy ACC / motion override).
    /// Decision-engine Redis cooldown is a second layer; this blocks GpsAlertEvents spam.
    /// </summary>
    private const int IgnitionDedupMinutes = 2;

    private async Task EvaluateIgnitionTransitionAsync(
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

        // Suppress rapid ON↔OFF flaps (miswired ACC / motion-forced OFF).
        var recentSame = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = @EventType AND IsDeleted = 0
                  AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
              ) THEN 1 ELSE 0 END",
            new { dto.VehicleId, EventType = eventType, Minutes = IgnitionDedupMinutes, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recentSame)
        {
            return;
        }

        var alertId = await InsertAlertAsync(connection, null, dto, null, eventType, message, timestamp, cancellationToken);
        if (alertId <= 0)
            return;

        var vehicle = await connection.QueryFirstOrDefaultAsync<(int? TenantId, string? PlateNumber, string? Name)>(
            new CommandDefinition(
                "SELECT TenantId, RegistrationNumber AS PlateNumber, Name FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = dto.VehicleId },
                cancellationToken: cancellationToken));

        if (vehicle.TenantId is null or <= 0)
        {
            logger.LogDebug(
                "Ignition {EventType} alert {AlertId} stored but notification skipped — vehicle {VehicleId} has no TenantId",
                eventType, alertId, dto.VehicleId);
            return;
        }

        var label = !string.IsNullOrWhiteSpace(vehicle.PlateNumber)
            ? vehicle.PlateNumber.Trim()
            : !string.IsNullOrWhiteSpace(vehicle.Name)
                ? vehicle.Name.Trim()
                : $"#{dto.VehicleId}";

        var title = dto.Ignition.Value ? "Ignition ON" : "Ignition OFF";
        var body = dto.Ignition.Value
            ? $"Vehicle {label}: ignition / ACC turned ON."
            : $"Vehicle {label}: ignition / ACC turned OFF.";

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            eventType,
            title,
            body,
            NotificationType.EngineCommandSent,
            ReferenceId: dto.VehicleId,
            AlertEventId: alertId,
            TenantId: vehicle.TenantId,
            SuggestedPriority: 3,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser,
                NotificationChannels.Push, NotificationChannels.Sms
            ]), cancellationToken);
    }

    private static async Task<int?> ResolveVehicleTenantIdAsync(
        System.Data.IDbConnection connection,
        int vehicleId,
        CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
            new { Id = vehicleId },
            cancellationToken: cancellationToken));
    }

    private const decimal LowFuelThresholdPercent = 15m;

    private async Task EvaluateLowFuelAsync(
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

    private async Task EvaluatePowerCutAsync(
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

    private async Task EvaluateGpsUnreliableAsync(
        System.Data.IDbConnection connection,
        IngestPositionDto dto,
        GpsPositionIngestPlan plan,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var dedup = gpsSettings.Value.PositionQuality.GpsUnreliableAlertDedupMinutes;
        if (dedup <= 0)
            dedup = 30;

        var recentAlert = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = 'gps_unreliable' AND IsDeleted = 0
                AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
              ) THEN 1 ELSE 0 END",
            new { dto.VehicleId, Minutes = dedup, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recentAlert)
            return;

        var detail = plan.QualityCode switch
        {
            GpsPositionQualityEvaluator.QualityFrozenCoordinates =>
                "GPS coordinates unchanged while the tracker reports movement — location may be stale.",
            GpsPositionQualityEvaluator.QualityImpossibleTeleport =>
                "GPS reported an implausible jump — location may be wrong.",
            GpsPositionQualityEvaluator.QualityLbsOnly =>
                "Cell-tower (LBS) fix — GPS accuracy is limited.",
            GpsPositionQualityEvaluator.QualityTraccarOutdated =>
                "Tracker fix is outdated — location may not reflect current position.",
            _ => "GPS fix quality is unreliable — verify on map before dispatch decisions."
        };

        await InsertAlertAsync(connection, null, dto, null, "gps_unreliable", detail, timestamp, cancellationToken);
    }

    private async Task EvaluateGpsLostAsync(
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

    private Task<int> InsertAlertAsync(
        System.Data.IDbConnection connection,
        int? ruleId,
        IngestPositionDto dto,
        int? geofenceId,
        string eventType,
        string message,
        DateTime timestamp,
        CancellationToken cancellationToken)
        => _alertWriter.InsertAsync(
            connection, dto.VehicleId, dto.Latitude, dto.Longitude, dto.Speed,
            eventType, message, timestamp, ruleId, geofenceId, dto.DriverId,
            cancellationToken: cancellationToken);

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
}
