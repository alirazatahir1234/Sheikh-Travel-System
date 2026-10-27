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
/// <para>
/// Gmeet devices: additionally SMS the device SOS number (SosNumber → phone-like Contact)
/// via Notifications:Sms — separate from Admin/Dispatcher ignition SMS recipients.
/// </para>
/// </summary>
public class GpsOfflineDetectionHostedService(
    IServiceProvider serviceProvider,
    IOptions<GpsSettings> gpsSettings,
    ILogger<GpsOfflineDetectionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan DefaultScanInterval = TimeSpan.FromMinutes(5);

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
                    await Task.Delay(ResolveScanInterval(), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private TimeSpan ResolveScanInterval()
    {
        var gmeet = gpsSettings.Value.GmeetOfflineSms;
        if (gmeet is { Enabled: true, ScanIntervalSeconds: > 0 })
            return TimeSpan.FromSeconds(Math.Clamp(gmeet.ScanIntervalSeconds, 30, 600));
        return DefaultScanInterval;
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var decisionEngine = scope.ServiceProvider.GetRequiredService<INotificationDecisionEngine>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var escalation = scope.ServiceProvider.GetRequiredService<IEscalationService>();
        var alertWriter = scope.ServiceProvider.GetRequiredService<IGpsAlertWriter>();
        using var connection = dbFactory.CreateConnection();

        var staleMinutes = gpsSettings.Value.OfflineStaleMinutes;
        var gmeetOpts = gpsSettings.Value.GmeetOfflineSms;
        if (gmeetOpts.Enabled && gmeetOpts.StaleMinutes > 0)
            staleMinutes = gmeetOpts.StaleMinutes;

        // Once-until-online: suppress while any open (unacknowledged) vehicle_offline exists.
        // IngestPositionCommand acknowledges those rows when GPS resumes, allowing a fresh fire.
        var staleVehicles = (await connection.QueryAsync<StaleVehicleRow>(
            new CommandDefinition(
                """
                SELECT vcl.VehicleId, v.TenantId, vcl.Latitude, vcl.Longitude, vcl.Speed,
                       d.Id AS GpsDeviceId, d.Name AS DeviceName, d.Model, d.Vendor,
                       d.TrackerModelKey, d.Phone AS DeviceSimPhone, d.Contact, d.SosNumber,
                       m.CatalogKey, b.Name AS BrandName,
                       v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber
                FROM VehicleCurrentLocation vcl
                INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.IsDeleted = 0
                AND v.Status <> 5
                LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
                LEFT JOIN TrackerModels m ON m.Id = d.TrackerModelId
                LEFT JOIN TrackerBrands b ON b.Id = m.TrackerBrandId
                WHERE v.TenantId IS NOT NULL AND v.TenantId > 0
                  AND vcl.LastUpdate < DATEADD(MINUTE, -@StaleMinutes, GETUTCDATE())
                  AND NOT EXISTS (
                    SELECT 1 FROM GpsAlertEvents e
                    WHERE e.VehicleId = vcl.VehicleId AND e.EventType = 'vehicle_offline'
                      AND e.IsDeleted = 0
                      AND e.IsAcknowledged = 0
                  )
                """,
                new { StaleMinutes = staleMinutes },
                cancellationToken: cancellationToken))).ToList();

        var gmeetSmsCount = 0;
        foreach (var v in staleVehicles)
        {
            var alertId = await alertWriter.InsertAsync(
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
                TenantId: v.TenantId,
                SuggestedPriority: 3,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser,
                    NotificationChannels.Push, NotificationChannels.Sms
                ]), cancellationToken);

            await escalation.StartAsync(
                "vehicle_offline",
                v.VehicleId,
                alertEventId: alertId,
                tenantId: v.TenantId,
                cancellationToken: cancellationToken);

            if (await TrySendGmeetOfflineSosSmsAsync(
                    notifications, v, staleMinutes, alertId, gmeetOpts, cancellationToken))
                gmeetSmsCount++;
        }

        if (staleVehicles.Count > 0)
        {
            logger.LogInformation(
                "GPS offline-detection flagged {Count} vehicle(s); Gmeet SOS SMS queued/sent for {GmeetCount}.",
                staleVehicles.Count, gmeetSmsCount);
        }
    }

    private async Task<bool> TrySendGmeetOfflineSosSmsAsync(
        INotificationService notifications,
        StaleVehicleRow v,
        int staleMinutes,
        int alertId,
        GmeetOfflineSmsOptions opts,
        CancellationToken cancellationToken)
    {
        if (!opts.Enabled)
            return false;

        if (!GmeetDeviceMatcher.IsGmeet(
                v.CatalogKey,
                v.TrackerModelKey,
                v.BrandName,
                v.Vendor,
                v.Model,
                v.DeviceName,
                opts.ExtraNameContains))
            return false;

        var sos = GpsDeviceSosNumberResolver.Resolve(v.SosNumber, v.Contact, v.DeviceSimPhone);
        if (sos is null)
        {
            logger.LogWarning(
                "Gmeet device {DeviceId} (vehicle {VehicleId}) went offline but has no SosNumber/Contact phone — SMS skipped. Set GpsDevices.SosNumber.",
                v.GpsDeviceId, v.VehicleId);
            return false;
        }

        var label = !string.IsNullOrWhiteSpace(v.VehicleName)
            ? v.VehicleName
            : $"Vehicle #{v.VehicleId}";
        if (!string.IsNullOrWhiteSpace(v.PlateNumber))
            label = $"{label} ({v.PlateNumber})";

        var title = "Gmeet tracker offline";
        var message =
            $"{label} stopped reporting GPS for {staleMinutes}+ minutes (no data package / GPRS). " +
            $"Device: {v.DeviceName ?? v.GpsDeviceId?.ToString() ?? "unknown"}.";

        // Direct SMS to device SOS — not Admin Users.Phone. SendNow for near-real-time delivery.
        await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
            UserId: null,
            TenantId: v.TenantId,
            Title: title,
            Message: message,
            Type: NotificationType.VehicleOffline,
            ReferenceId: v.VehicleId,
            Priority: 3,
            Channel: NotificationChannels.Sms,
            RecipientType: "DeviceSos",
            TemplateKey: "gmeet_offline_sos",
            SendNow: true,
            Phone: sos,
            Module: "Fleet"), cancellationToken);

        logger.LogInformation(
            "Gmeet offline SOS SMS dispatched for vehicle {VehicleId} alert {AlertId} → {Phone}",
            v.VehicleId, alertId, GpsDeviceSosNumberResolver.Mask(sos));
        return true;
    }

    private sealed class StaleVehicleRow
    {
        public int VehicleId { get; init; }
        public int TenantId { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public decimal? Speed { get; init; }
        public int? GpsDeviceId { get; init; }
        public string? DeviceName { get; init; }
        public string? Model { get; init; }
        public string? Vendor { get; init; }
        public string? TrackerModelKey { get; init; }
        public string? DeviceSimPhone { get; init; }
        public string? Contact { get; init; }
        public string? SosNumber { get; init; }
        public string? CatalogKey { get; init; }
        public string? BrandName { get; init; }
        public string? VehicleName { get; init; }
        public string? PlateNumber { get; init; }
    }
}
