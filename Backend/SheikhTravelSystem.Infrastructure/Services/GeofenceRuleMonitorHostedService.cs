using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Geofence rules that need a clock rather than an incoming position:
/// <list type="bullet">
/// <item>Dwell: a parked vehicle often stops reporting, so an open visit past its geofence's dwell limit is caught
/// here. The visit's DwellBreached flag is claimed atomically, so the ingest path and this job never both alert.</item>
/// <item>"No assigned vehicle arrived by HH:MM": once per geofence per local day, logged in GeofenceNotArrivedLog.</item>
/// </list>
/// Both respect the geofence's schedule, the tenant's Geofencing add-on, and the tenant's time zone.
/// </summary>
public sealed class GeofenceRuleMonitorHostedService(
    IServiceProvider serviceProvider,
    ILogger<GeofenceRuleMonitorHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Geofence rule monitor scan failed.");
                }

                try
                {
                    await Task.Delay(ScanInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ScanAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var alertWriter = scope.ServiceProvider.GetRequiredService<IGpsAlertWriter>();
        var notifier = scope.ServiceProvider.GetRequiredService<IGeofenceAlertNotifier>();
        var zones = scope.ServiceProvider.GetRequiredService<ITenantTimeZoneResolver>();

        using var connection = dbFactory.CreateConnection();
        var tenantZones = new Dictionary<int, TimeZoneInfo>();

        async Task<DateTime> LocalAsync(int tenantId, DateTime utc)
        {
            if (!tenantZones.TryGetValue(tenantId, out var zone))
            {
                zone = await zones.GetTenantTimeZoneAsync(tenantId, cancellationToken);
                tenantZones[tenantId] = zone;
            }

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        }

        await ScanDwellAsync(connection, alertWriter, notifier, LocalAsync, nowUtc, cancellationToken);
        await ScanNotArrivedAsync(connection, notifier, LocalAsync, zones, nowUtc, cancellationToken);
    }

    private async Task ScanDwellAsync(
        System.Data.IDbConnection connection,
        IGpsAlertWriter alertWriter,
        IGeofenceAlertNotifier notifier,
        Func<int, DateTime, Task<DateTime>> localAsync,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var overdue = (await connection.QueryAsync<DwellRow>(new CommandDefinition(
            """
            SELECT TOP 200 gv.Id, gv.TenantId, gv.GeofenceId, gv.VehicleId, gv.DriverId, gv.EnteredUtc,
                   g.Name, g.DwellMinutes, g.ScheduleDays, g.ScheduleStart, g.ScheduleEnd,
                   COALESCE(NULLIF(LTRIM(RTRIM(v.RegistrationNumber)), N''), v.Name) AS VehicleLabel,
                   vcl.Latitude, vcl.Longitude, vcl.Speed
            FROM GeofenceVisits gv
            INNER JOIN Geofences g ON g.Id = gv.GeofenceId AND g.TenantId = gv.TenantId AND g.IsActive = 1 AND g.IsDeleted = 0
            INNER JOIN Vehicles v ON v.Id = gv.VehicleId AND v.TenantId = gv.TenantId AND v.IsDeleted = 0
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = gv.VehicleId
            WHERE gv.ExitedUtc IS NULL AND gv.DwellBreached = 0
              AND g.DwellMinutes > 0
              AND gv.EnteredUtc <= DATEADD(MINUTE, -g.DwellMinutes, @Now)
              AND EXISTS (SELECT 1 FROM TenantFeatures tf
                          WHERE tf.TenantId = gv.TenantId AND tf.FeatureKey = @FeatureKey AND tf.IsEnabled = 1)
            ORDER BY gv.EnteredUtc
            """,
            new { Now = nowUtc, FeatureKey = GpsAddOnFeatureCatalog.Geofencing },
            cancellationToken: cancellationToken))).ToList();

        foreach (var row in overdue)
        {
            var schedule = new GeofenceSchedule(row.ScheduleDays, row.ScheduleStart, row.ScheduleEnd);
            if (!schedule.IsAlways && !schedule.IsActiveAt(await localAsync(row.TenantId, nowUtc)))
                continue;

            // Claim the visit so the ingest path (or a second instance) cannot alert for it as well.
            var claimed = await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE GeofenceVisits SET DwellBreached = 1 WHERE Id = @Id AND TenantId = @TenantId AND DwellBreached = 0",
                new { row.Id, row.TenantId },
                cancellationToken: cancellationToken));
            if (claimed == 0)
                continue;

            if (row.Latitude is not double lat || row.Longitude is not double lng)
                continue;

            var label = string.IsNullOrWhiteSpace(row.VehicleLabel) ? $"Vehicle #{row.VehicleId}" : row.VehicleLabel;
            var message = $"{label} has been inside {row.Name} for over {row.DwellMinutes} minutes.";
            var alertId = await alertWriter.InsertAsync(
                connection, row.VehicleId, lat, lng, row.Speed ?? 0, GeofenceRuleEvents.Dwell, message, nowUtc,
                geofenceId: row.GeofenceId, driverId: row.DriverId, cancellationToken: cancellationToken);
            if (alertId <= 0)
                continue;

            await notifier.NotifyAsync(new GeofenceAlertNotice(
                row.TenantId, row.GeofenceId, row.VehicleId, row.DriverId, alertId, GeofenceRuleEvents.Dwell,
                "Overstay in geofence", message, 3,
                [NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push]), cancellationToken);
        }
    }

    private async Task ScanNotArrivedAsync(
        System.Data.IDbConnection connection,
        IGeofenceAlertNotifier notifier,
        Func<int, DateTime, Task<DateTime>> localAsync,
        ITenantTimeZoneResolver zones,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // Only geofences that some vehicle is meant to reach: all vehicles, or at least one assignment.
        var candidates = (await connection.QueryAsync<NotArrivedRow>(new CommandDefinition(
            """
            SELECT g.Id, g.TenantId, g.Name, g.NotArrivedBy, g.ScheduleDays, g.ScheduleStart, g.ScheduleEnd
            FROM Geofences g
            WHERE g.IsActive = 1 AND g.IsDeleted = 0 AND g.NotArrivedBy IS NOT NULL
              AND (g.AppliesToAll = 1 OR EXISTS (SELECT 1 FROM GeofenceAssignments a WHERE a.GeofenceId = g.Id AND a.IsDeleted = 0))
              AND EXISTS (SELECT 1 FROM TenantFeatures tf
                          WHERE tf.TenantId = g.TenantId AND tf.FeatureKey = @FeatureKey AND tf.IsEnabled = 1)
            """,
            new { FeatureKey = GpsAddOnFeatureCatalog.Geofencing },
            cancellationToken: cancellationToken))).ToList();

        foreach (var row in candidates)
        {
            var nowLocal = await localAsync(row.TenantId, nowUtc);
            var rules = GeofenceRules.None with
            {
                NotArrivedBy = row.NotArrivedBy,
                Schedule = new GeofenceSchedule(row.ScheduleDays, row.ScheduleStart, row.ScheduleEnd)
            };

            // Cheap check first; the arrival lookup only runs once the deadline has passed.
            if (!GeofenceRuleEvaluator.IsNotArrivedDue(rules, nowLocal, anyArrivalToday: false))
                continue;

            var timeZone = await zones.GetTenantTimeZoneAsync(row.TenantId, cancellationToken);
            var localDate = DateOnly.FromDateTime(nowLocal);
            var dayStartUtc = TenantTimeZoneHelper.LocalDayStartUtc(localDate, timeZone);

            var arrived = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                """
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM GeofenceVisits
                    WHERE GeofenceId = @GeofenceId AND TenantId = @TenantId
                      AND (EnteredUtc >= @DayStartUtc OR ExitedUtc IS NULL OR ExitedUtc >= @DayStartUtc)
                ) THEN 1 ELSE 0 END AS BIT)
                """,
                new { GeofenceId = row.Id, row.TenantId, DayStartUtc = dayStartUtc },
                cancellationToken: cancellationToken));
            if (arrived)
                continue;

            var logged = await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO GeofenceNotArrivedLog (TenantId, GeofenceId, LocalDate)
                SELECT @TenantId, @GeofenceId, @LocalDate
                WHERE NOT EXISTS (SELECT 1 FROM GeofenceNotArrivedLog
                                  WHERE GeofenceId = @GeofenceId AND LocalDate = @LocalDate AND TenantId = @TenantId)
                """,
                new { row.TenantId, GeofenceId = row.Id, LocalDate = localDate.ToDateTime(TimeOnly.MinValue) },
                cancellationToken: cancellationToken));
            if (logged == 0)
                continue;

            await notifier.NotifyAsync(new GeofenceAlertNotice(
                row.TenantId, row.Id, VehicleId: 0, DriverId: null, AlertEventId: null, GeofenceRuleEvents.NotArrived,
                "No vehicle arrived",
                $"No assigned vehicle has arrived at {row.Name} by {row.NotArrivedBy:hh\\:mm}.",
                Priority: 4,
                DefaultChannels: [NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push],
                ReferenceId: row.Id), cancellationToken);
        }
    }

    private sealed class DwellRow
    {
        public long Id { get; init; }
        public int TenantId { get; init; }
        public int GeofenceId { get; init; }
        public int VehicleId { get; init; }
        public int? DriverId { get; init; }
        public DateTime EnteredUtc { get; init; }
        public string Name { get; init; } = string.Empty;
        public int? DwellMinutes { get; init; }
        public byte? ScheduleDays { get; init; }
        public TimeSpan? ScheduleStart { get; init; }
        public TimeSpan? ScheduleEnd { get; init; }
        public string? VehicleLabel { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public decimal? Speed { get; init; }
    }

    private sealed class NotArrivedRow
    {
        public int Id { get; init; }
        public int TenantId { get; init; }
        public string Name { get; init; } = string.Empty;
        public TimeSpan? NotArrivedBy { get; init; }
        public byte? ScheduleDays { get; init; }
        public TimeSpan? ScheduleStart { get; init; }
        public TimeSpan? ScheduleEnd { get; init; }
    }
}
