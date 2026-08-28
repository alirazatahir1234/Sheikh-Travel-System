using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

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
    IOptions<GpsSettings> gpsSettings,
    ILogger<GpsOfflineDetectionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);

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
        var decisionEngine = scope.ServiceProvider.GetRequiredService<INotificationDecisionEngine>();
        var escalation = scope.ServiceProvider.GetRequiredService<IEscalationService>();
        using var connection = dbFactory.CreateConnection();

        var staleMinutes = gpsSettings.Value.OfflineStaleMinutes;
        var cooldownMinutes = gpsSettings.Value.OfflineAlertCooldownMinutes <= 0
            ? 120
            : gpsSettings.Value.OfflineAlertCooldownMinutes;

        var staleVehicles = (await connection.QueryAsync<(int VehicleId, double Latitude, double Longitude, decimal? Speed)>(
            new CommandDefinition(
                """
                SELECT vcl.VehicleId, vcl.Latitude, vcl.Longitude, vcl.Speed
                FROM VehicleCurrentLocation vcl
                INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.IsDeleted = 0
                AND v.Status <> 5
                WHERE vcl.LastUpdate < DATEADD(MINUTE, -@StaleMinutes, GETUTCDATE())
                  AND NOT EXISTS (
                    SELECT 1 FROM GpsAlertEvents e
                    WHERE e.VehicleId = vcl.VehicleId AND e.EventType = 'vehicle_offline'
                      AND e.IsDeleted = 0
                      AND e.Timestamp > DATEADD(MINUTE, -@CooldownMinutes, GETUTCDATE())
                  )
                """,
                new { StaleMinutes = staleMinutes, CooldownMinutes = cooldownMinutes },
                cancellationToken: cancellationToken))).ToList();

        foreach (var v in staleVehicles)
        {
            var alertId = await GpsAlertWriter.InsertAsync(
                connection,
                v.VehicleId,
                v.Latitude,
                v.Longitude,
                v.Speed ?? 0,
                "vehicle_offline",
                "No GPS data received. Vehicle has stopped reporting.",
                DateTime.UtcNow,
                cancellationToken: cancellationToken);
            if (alertId <= 0)
                continue;

            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "vehicle_offline",
                "Vehicle offline",
                $"Vehicle #{v.VehicleId} has not reported GPS for {staleMinutes}+ minutes.",
                NotificationType.VehicleOffline,
                ReferenceId: v.VehicleId,
                AlertEventId: alertId,
                SuggestedPriority: 3,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser,
                    NotificationChannels.Push, NotificationChannels.Sms
                ]), cancellationToken);

            await escalation.StartAsync("vehicle_offline", v.VehicleId, alertEventId: alertId, cancellationToken: cancellationToken);
        }

        if (staleVehicles.Count > 0)
        {
            logger.LogInformation("GPS offline-detection flagged {Count} vehicle(s).", staleVehicles.Count);
        }
    }
}
