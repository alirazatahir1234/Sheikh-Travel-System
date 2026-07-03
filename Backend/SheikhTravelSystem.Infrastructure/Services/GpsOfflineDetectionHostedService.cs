using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Periodically flags vehicles that have gone quiet — unlike the other GpsAlertEvents detectors,
/// this is the absence of new data rather than something triggered by an incoming position, so it
/// needs its own tick rather than living in IngestPositionCommand. Dedups via IsAcknowledged = 0
/// (not a time window, since "still offline" can span many ticks); IngestPositionCommand
/// acknowledges the matching row again once a position actually arrives.
/// </summary>
public class GpsOfflineDetectionHostedService(
    IServiceProvider serviceProvider,
    ILogger<GpsOfflineDetectionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);
    private const int OfflineStaleMinutes = 30;

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
                    logger.LogError(ex, "GPS offline-detection scan failed.");
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

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var connection = dbFactory.CreateConnection();

        var staleVehicles = (await connection.QueryAsync<(int VehicleId, double Latitude, double Longitude, decimal? Speed)>(
            new CommandDefinition(
                """
                SELECT vcl.VehicleId, vcl.Latitude, vcl.Longitude, vcl.Speed
                FROM VehicleCurrentLocation vcl
                INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.IsDeleted = 0
                WHERE vcl.LastUpdate < DATEADD(MINUTE, -@OfflineStaleMinutes, GETUTCDATE())
                  AND NOT EXISTS (
                    SELECT 1 FROM GpsAlertEvents e
                    WHERE e.VehicleId = vcl.VehicleId AND e.EventType = 'vehicle_offline'
                      AND e.IsAcknowledged = 0 AND e.IsDeleted = 0
                  )
                """,
                new { OfflineStaleMinutes },
                cancellationToken: cancellationToken))).ToList();

        foreach (var v in staleVehicles)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO GpsAlertEvents
                (RuleId, VehicleId, GeofenceId, EventType, Latitude, Longitude, Speed, Message, Timestamp, CreatedAt, IsDeleted)
                VALUES (NULL, @VehicleId, NULL, 'vehicle_offline', @Latitude, @Longitude, @Speed, 'Vehicle went offline', GETUTCDATE(), GETUTCDATE(), 0)
                """,
                new { v.VehicleId, v.Latitude, v.Longitude, Speed = v.Speed ?? 0 },
                cancellationToken: cancellationToken));
        }

        if (staleVehicles.Count > 0)
        {
            logger.LogInformation("GPS offline-detection flagged {Count} vehicle(s).", staleVehicles.Count);
        }
    }
}
