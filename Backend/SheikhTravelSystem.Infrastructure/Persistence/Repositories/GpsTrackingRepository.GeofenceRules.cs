using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Geofence visits and rules evaluated on every position (see <see cref="GeofenceVisitTracker"/> and
/// <see cref="GeofenceRuleEvaluator"/> for the rules themselves). Dwell for vehicles that stop reporting, and
/// "no vehicle arrived by", run in GeofenceRuleMonitorHostedService.
/// </summary>
public sealed partial class GpsTrackingRepository
{
    /// <summary>Command queued when a geofence with "engine block on exit" is left (never sent without approval).</summary>
    private const string GeofenceEngineBlockCommand = "engineStop";

    private sealed record ZoneVisitContext(
        int TenantId,
        WatchedZoneRow Zone,
        GeofenceRules Rules,
        IngestPositionDto Position,
        int? DriverId,
        DateTime Timestamp);

    /// <summary>Opens, updates or closes this vehicle's visit to the zone and applies the zone's inside/exit rules.</summary>
    private async Task TrackGeofenceVisitAsync(
        System.Data.IDbConnection connection,
        ZoneVisitContext ctx,
        GeofenceEvaluation evaluation,
        Func<GeofenceRules, DateTime, Task<bool>> rulesActiveAt,
        CancellationToken cancellationToken)
    {
        var zone = ctx.Zone;

        if (evaluation.Transition == GeofenceTransition.Exit)
        {
            var exitedUtc = evaluation.State.SinceUtc;
            var afterHours = !ctx.Rules.Schedule.IsAlways && !await rulesActiveAt(ctx.Rules, exitedUtc);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE GeofenceVisits
                SET ExitedUtc = CASE WHEN @ExitedUtc < EnteredUtc THEN EnteredUtc ELSE @ExitedUtc END,
                    AfterHoursExit = @AfterHours
                WHERE VehicleId = @VehicleId AND GeofenceId = @GeofenceId AND ExitedUtc IS NULL AND TenantId = @TenantId
                """,
                new { ExitedUtc = exitedUtc, AfterHours = afterHours, ctx.Position.VehicleId, GeofenceId = zone.Id, ctx.TenantId },
                cancellationToken: cancellationToken));

            await ApplyExitRulesAsync(connection, ctx, exitedUtc, afterHours, rulesActiveAt, cancellationToken);
            return;
        }

        if (!evaluation.State.IsInside)
            return;

        GeofenceVisitState visit;
        long visitId;
        if (zone.VisitId is long openId && zone.VisitEnteredUtc is DateTime enteredUtc)
        {
            visitId = openId;
            visit = GeofenceVisitTracker.Observe(
                new GeofenceVisitState(
                    enteredUtc,
                    zone.VisitLastPositionUtc ?? enteredUtc,
                    zone.VisitIdleSeconds ?? 0,
                    zone.VisitIdleSinceUtc,
                    zone.VisitMaxSpeedKmh ?? 0,
                    zone.VisitDwellBreached ?? false,
                    zone.VisitSpeedBreached ?? false,
                    zone.VisitIdleBreached ?? false),
                ctx.Timestamp,
                ctx.Position.Speed,
                ctx.Position.Ignition);
        }
        else
        {
            // A fresh entry, or a vehicle that was already inside before visits were recorded: the state machine's
            // SinceUtc is when it was first seen inside.
            visit = GeofenceVisitTracker.Open(evaluation.State.SinceUtc, ctx.Timestamp, ctx.Position.Speed, ctx.Position.Ignition);
            var opened = await OpenGeofenceVisitAsync(connection, ctx, visit, cancellationToken);
            if (opened is not long newId)
                return;
            visitId = newId;
        }

        if (await rulesActiveAt(ctx.Rules, ctx.Timestamp))
            visit = await ApplyInsideRulesAsync(connection, ctx, visit, cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE GeofenceVisits
            SET LastPositionUtc = @LastPositionUtc, IdleSeconds = @IdleSeconds, IdleSinceUtc = @IdleSinceUtc,
                MaxSpeedKmh = @MaxSpeedKmh, DwellBreached = @DwellBreached, SpeedBreached = @SpeedBreached,
                IdleBreached = @IdleBreached, DriverId = COALESCE(DriverId, @DriverId)
            WHERE Id = @Id AND TenantId = @TenantId
            """,
            new
            {
                Id = visitId,
                ctx.TenantId,
                visit.LastPositionUtc,
                visit.IdleSeconds,
                visit.IdleSinceUtc,
                MaxSpeedKmh = Math.Min(visit.MaxSpeedKmh, 99999.9m),
                visit.DwellBreached,
                visit.SpeedBreached,
                visit.IdleBreached,
                ctx.DriverId
            },
            cancellationToken: cancellationToken));
    }

    private async Task<long?> OpenGeofenceVisitAsync(
        System.Data.IDbConnection connection,
        ZoneVisitContext ctx,
        GeofenceVisitState visit,
        CancellationToken cancellationToken)
    {
        var args = new
        {
            ctx.TenantId,
            GeofenceId = ctx.Zone.Id,
            ctx.Position.VehicleId,
            ctx.DriverId,
            visit.EnteredUtc,
            visit.LastPositionUtc,
            visit.IdleSinceUtc,
            MaxSpeedKmh = Math.Min(visit.MaxSpeedKmh, 99999.9m)
        };

        try
        {
            return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                """
                IF NOT EXISTS (SELECT 1 FROM GeofenceVisits
                               WHERE VehicleId = @VehicleId AND GeofenceId = @GeofenceId AND ExitedUtc IS NULL AND TenantId = @TenantId)
                    INSERT INTO GeofenceVisits
                        (TenantId, GeofenceId, VehicleId, DriverId, EnteredUtc, LastPositionUtc, IdleSinceUtc, MaxSpeedKmh)
                    VALUES (@TenantId, @GeofenceId, @VehicleId, @DriverId, @EnteredUtc, @LastPositionUtc, @IdleSinceUtc, @MaxSpeedKmh);

                SELECT Id FROM GeofenceVisits
                WHERE VehicleId = @VehicleId AND GeofenceId = @GeofenceId AND ExitedUtc IS NULL AND TenantId = @TenantId;
                """,
                args,
                cancellationToken: cancellationToken));
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // A concurrent fix opened it first (one open visit per vehicle and zone).
            return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                """
                SELECT Id FROM GeofenceVisits
                WHERE VehicleId = @VehicleId AND GeofenceId = @GeofenceId AND ExitedUtc IS NULL AND TenantId = @TenantId
                """,
                args,
                cancellationToken: cancellationToken));
        }
    }

    private async Task<GeofenceVisitState> ApplyInsideRulesAsync(
        System.Data.IDbConnection connection,
        ZoneVisitContext ctx,
        GeofenceVisitState visit,
        CancellationToken cancellationToken)
    {
        var zone = ctx.Zone;

        if (GeofenceRuleEvaluator.IsSpeedBreach(ctx.Rules, ctx.Position.Speed))
        {
            visit = visit with { SpeedBreached = true };
            await RaiseGeofenceRuleAlertAsync(connection, ctx, GeofenceRuleEvents.Overspeed, "Speed inside geofence",
                $"{{vehicle}} drove {ctx.Position.Speed:0} km/h inside {zone.Name} (limit {ctx.Rules.SpeedLimitKmh:0} km/h).",
                priority: 3, cancellationToken);
        }

        if (GeofenceRuleEvaluator.IsIdleBreach(ctx.Rules, visit, ctx.Timestamp))
        {
            visit = visit with { IdleBreached = true };
            await RaiseGeofenceRuleAlertAsync(connection, ctx, GeofenceRuleEvents.Idle, "Idling inside geofence",
                $"{{vehicle}} has idled with the engine on inside {zone.Name} for over {ctx.Rules.IdleMinutes} minutes.",
                priority: 3, cancellationToken);
        }

        if (GeofenceRuleEvaluator.IsDwellBreach(ctx.Rules, visit, ctx.Timestamp))
        {
            visit = visit with { DwellBreached = true };
            await RaiseGeofenceRuleAlertAsync(connection, ctx, GeofenceRuleEvents.Dwell, "Overstay in geofence",
                $"{{vehicle}} has been inside {zone.Name} for over {ctx.Rules.DwellMinutes} minutes.",
                priority: 3, cancellationToken);
        }

        return visit;
    }

    private async Task ApplyExitRulesAsync(
        System.Data.IDbConnection connection,
        ZoneVisitContext ctx,
        DateTime exitedUtc,
        bool afterHours,
        Func<GeofenceRules, DateTime, Task<bool>> rulesActiveAt,
        CancellationToken cancellationToken)
    {
        var zone = ctx.Zone;

        if (afterHours && ctx.Rules.AfterHoursExitCritical)
        {
            await RaiseGeofenceRuleAlertAsync(connection, ctx, GeofenceRuleEvents.AfterHoursExit, "Left geofence after hours",
                $"{{vehicle}} left {zone.Name} outside its active hours.", priority: 4, cancellationToken);
        }

        if (!ctx.Rules.EngineBlockOnExit || !await rulesActiveAt(ctx.Rules, exitedUtc))
            return;

        var queued = await automatedCommands.QueueForApprovalAsync(
            ctx.TenantId, ctx.Position.VehicleId, GeofenceEngineBlockCommand,
            $"Left geofence {zone.Name}", "system:geofence", cancellationToken);

        if (!queued.Success)
        {
            logger.LogInformation(
                "Geofence {GeofenceId}: engine stop not queued for vehicle {VehicleId}: {Reason}",
                zone.Id, ctx.Position.VehicleId, queued.Message);
            return;
        }

        var label = await VehicleLabelAsync(connection, ctx, cancellationToken);
        await geofenceNotifier.NotifyAsync(new GeofenceAlertNotice(
            ctx.TenantId, zone.Id, ctx.Position.VehicleId, ctx.DriverId, null,
            "geofence_engine_block_requested",
            "Engine stop waiting for approval",
            $"{label} left {zone.Name}. An engine stop is waiting in Device Commands for a manager to approve.",
            Priority: 4,
            DefaultChannels: [NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push]),
            cancellationToken);
    }

    /// <summary>
    /// Writes the alert (unless the same alert for this vehicle and zone fired within the zone's cooldown) and
    /// notifies. <c>{vehicle}</c> in <paramref name="message"/> becomes the plate or name.
    /// </summary>
    private async Task RaiseGeofenceRuleAlertAsync(
        System.Data.IDbConnection connection,
        ZoneVisitContext ctx,
        string eventType,
        string title,
        string message,
        int priority,
        CancellationToken cancellationToken)
    {
        var cooldown = Math.Max(1, ctx.Rules.CooldownMinutes);
        var lastSame = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            """
            SELECT MAX(Timestamp) FROM GpsAlertEvents
            WHERE VehicleId = @VehicleId AND GeofenceId = @GeofenceId AND EventType = @EventType AND IsDeleted = 0
              AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
            """,
            new { ctx.Position.VehicleId, GeofenceId = ctx.Zone.Id, EventType = eventType, Minutes = cooldown, ctx.Timestamp },
            cancellationToken: cancellationToken));

        if (GeofenceRuleEvaluator.IsInCooldown(lastSame, ctx.Timestamp, cooldown))
            return;

        var label = await VehicleLabelAsync(connection, ctx, cancellationToken);
        var text = message.Replace("{vehicle}", label, StringComparison.Ordinal);
        var alertId = await InsertAlertAsync(connection, null, ctx.Position, ctx.Zone.Id, eventType, text, ctx.Timestamp, cancellationToken);
        if (alertId <= 0)
            return;

        await geofenceNotifier.NotifyAsync(new GeofenceAlertNotice(
            ctx.TenantId, ctx.Zone.Id, ctx.Position.VehicleId, ctx.DriverId, alertId, eventType, title, text, priority,
            [NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push]), cancellationToken);
    }

    private static async Task<string> VehicleLabelAsync(
        System.Data.IDbConnection connection, ZoneVisitContext ctx, CancellationToken cancellationToken)
    {
        var label = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT COALESCE(NULLIF(LTRIM(RTRIM(RegistrationNumber)), N''), Name)
            FROM Vehicles WHERE Id = @VehicleId AND TenantId = @TenantId
            """,
            new { ctx.Position.VehicleId, ctx.TenantId },
            cancellationToken: cancellationToken));
        return string.IsNullOrWhiteSpace(label) ? $"Vehicle #{ctx.Position.VehicleId}" : label;
    }
}
