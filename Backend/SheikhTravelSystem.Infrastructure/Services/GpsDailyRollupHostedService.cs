using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Nightly rollup of GpsTrips/GpsAlertEvents into GpsVehicleDailyStats — backs the Analytics Trends
/// section (long-range charts) without scanning 90-day-purged GpsPositions or recomputing
/// fleet-wide sums from GpsTrips on every request. Populated purely from locally-persisted data,
/// never Traccar — a per-vehicle-per-day Traccar fetch would multiply the fan-out cost that was
/// already removed for whole-range queries elsewhere in this module. Consequence: HarshBrakeCount/
/// HarshAccelCount stay null forever (no local event type exists for them, only OverspeedCount does
/// via GpsAlertEvents) — an accepted, documented gap, not a bug.
///
/// Runs a backfill/catch-up pass covering any day (within the last 90) missing a rollup row, then
/// ticks daily — this means a fresh deploy or a missed tick both self-heal on the next run rather
/// than needing a separate one-off migration script.
/// </summary>
public class GpsDailyRollupHostedService(
    IServiceProvider serviceProvider,
    ILogger<GpsDailyRollupHostedService> logger) : BackgroundService
{
    private const int BackfillLookbackDays = 90;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "GPS daily rollup failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = dbFactory.CreateConnection();

        var earliestMissingDate = await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            """
            SELECT MIN(CAST(t.StartTime AS DATE))
            FROM GpsTrips t
            WHERE t.StartTime >= DATEADD(DAY, -@LookbackDays, GETUTCDATE())
              AND NOT EXISTS (
                SELECT 1 FROM GpsVehicleDailyStats s
                WHERE s.VehicleId = t.VehicleId AND s.StatDate = CAST(t.StartTime AS DATE)
              )
            """,
            new { LookbackDays = BackfillLookbackDays },
            cancellationToken: cancellationToken));

        if (earliestMissingDate is null)
        {
            return;
        }

        var today = DateTime.UtcNow.Date;
        var processed = 0;
        for (var date = earliestMissingDate.Value.Date; date < today; date = date.AddDays(1))
        {
            await RollupDayAsync(connection, date, cancellationToken);
            processed++;
        }

        if (processed > 0)
        {
            logger.LogInformation("GPS daily rollup processed {Count} day(s) through {Date}.", processed, today.AddDays(-1));
        }
    }

    private static async Task RollupDayAsync(System.Data.IDbConnection connection, DateTime date, CancellationToken cancellationToken)
    {
        var nextDate = date.AddDays(1);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            MERGE GpsVehicleDailyStats AS target
            USING (
                SELECT v.TenantId, t.VehicleId, CAST(t.StartTime AS DATE) AS StatDate,
                       SUM(t.DistanceKm) AS DistanceKm, COUNT(*) AS TripCount, SUM(t.DurationMinutes) AS DrivingMinutes,
                       AVG(t.AvgSpeedKmh) AS AvgSpeedKmh, MAX(t.MaxSpeedKmh) AS MaxSpeedKmh
                FROM GpsTrips t
                INNER JOIN Vehicles v ON v.Id = t.VehicleId AND v.IsDeleted = 0
                WHERE t.StartTime >= @Date AND t.StartTime < @NextDate
                GROUP BY v.TenantId, t.VehicleId, CAST(t.StartTime AS DATE)
            ) AS src
            ON target.VehicleId = src.VehicleId AND target.StatDate = src.StatDate
            WHEN MATCHED THEN
                UPDATE SET DistanceKm = src.DistanceKm, TripCount = src.TripCount, DrivingMinutes = src.DrivingMinutes,
                           AvgSpeedKmh = src.AvgSpeedKmh, MaxSpeedKmh = src.MaxSpeedKmh, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (TenantId, VehicleId, StatDate, DistanceKm, TripCount, DrivingMinutes, AvgSpeedKmh, MaxSpeedKmh, CreatedAt)
                VALUES (src.TenantId, src.VehicleId, src.StatDate, src.DistanceKm, src.TripCount, src.DrivingMinutes, src.AvgSpeedKmh, src.MaxSpeedKmh, GETUTCDATE());
            """,
            new { Date = date, NextDate = nextDate },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE s
            SET s.OverspeedCount = agg.OverspeedCount
            FROM GpsVehicleDailyStats s
            INNER JOIN (
                SELECT VehicleId, COUNT(*) AS OverspeedCount
                FROM GpsAlertEvents
                WHERE EventType IN ('overspeed', 'speed_exceeded') AND IsDeleted = 0
                  AND Timestamp >= @Date AND Timestamp < @NextDate
                GROUP BY VehicleId
            ) agg ON agg.VehicleId = s.VehicleId
            WHERE s.StatDate = @Date
            """,
            new { Date = date, NextDate = nextDate },
            cancellationToken: cancellationToken));
    }
}
