using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Hourly snapshot of local fleet-status counts (GpsFleetStatusCalculator — the same computation
/// GetGpsFleetStatusLocalQuery uses on-demand) into GpsFleetStatusSnapshots, powering the Live Map
/// screen's KPI sparklines/trend deltas and the Fleet Overview trend chart. Hourly rather than daily
/// so a handful of points can still form a meaningful sparkline and a missed tick doesn't lose a
/// whole day; the trend chart aggregates these up to daily buckets on the frontend.
/// </summary>
public class GpsFleetStatusSnapshotHostedService(
    IServiceProvider serviceProvider,
    ILogger<GpsFleetStatusSnapshotHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "GPS fleet-status snapshot scan failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = dbFactory.CreateConnection();

        var tenants = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT DISTINCT TenantId FROM Vehicles WHERE TenantId IS NOT NULL AND IsDeleted = 0",
            cancellationToken: cancellationToken))).ToList();

        var snapshotAt = DateTime.UtcNow;

        foreach (var tenantId in tenants)
        {
            var status = await GpsFleetStatusCalculator.ComputeAsync(connection, tenantId, cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO GpsFleetStatusSnapshots
                (TenantId, SnapshotAt, TotalVehicles, Online, Offline, Moving, Idle, Parked, NeverSeen, Sos, AlertsToday, CreatedAt)
                VALUES
                (@TenantId, @SnapshotAt, @TotalVehicles, @Online, @Offline, @Moving, @Idle, @Parked, @NeverSeen, @Sos, @AlertsToday, GETUTCDATE())
                """,
                new
                {
                    TenantId = tenantId,
                    SnapshotAt = snapshotAt,
                    status.TotalVehicles,
                    status.Online,
                    status.Offline,
                    status.Moving,
                    status.Idle,
                    status.Parked,
                    status.NeverSeen,
                    status.Sos,
                    status.AlertsToday
                },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation("GPS fleet-status snapshot recorded for {TenantCount} tenant(s) at {SnapshotAt}.", tenants.Count, snapshotAt);
    }
}
