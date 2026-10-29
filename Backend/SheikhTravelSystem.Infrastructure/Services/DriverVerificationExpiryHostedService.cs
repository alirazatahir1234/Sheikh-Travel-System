using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Marks expired driver docs, sends expiry/rejection reminders to drivers (push + in-app).
/// </summary>
public class DriverVerificationExpiryHostedService(
    IServiceProvider serviceProvider,
    IOptions<DriverVerificationOptions> options,
    ILogger<DriverVerificationExpiryHostedService> logger) : BackgroundService
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
                    logger.LogError(ex, "Driver verification expiry scan failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
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
        var driverRepo = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var alertDays = Math.Clamp(options.Value.ExpiryAlertDays, 7, 90);
        var threshold = DateTime.UtcNow.Date.AddDays(alertDays);

        using var connection = dbFactory.CreateConnection();
        var tenants = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT DISTINCT TenantId FROM Drivers WHERE TenantId IS NOT NULL AND IsDeleted = 0",
            cancellationToken: cancellationToken));

        foreach (var tenantId in tenants)
        {
            var drivers = await driverRepo.GetDriversForVerificationExpiryScanAsync(tenantId, cancellationToken);
            foreach (var d in drivers)
            {
                if (d.LicenseExpiry.Date < DateTime.UtcNow.Date
                    && !string.Equals(d.VerificationStatus, "ExpiredDocs", StringComparison.OrdinalIgnoreCase))
                {
                    await driverRepo.MarkDriverVerificationExpiredDocsAsync(tenantId, d.DriverId, cancellationToken);
                }

                if (!d.UserId.HasValue) continue;

                var licenseExpiring = d.LicenseExpiry.Date <= threshold && d.LicenseExpiry.Date >= DateTime.UtcNow.Date;
                var licenseExpired = d.LicenseExpiry.Date < DateTime.UtcNow.Date;
                var rejected = string.Equals(d.VerificationStatus, "Rejected", StringComparison.OrdinalIgnoreCase);

                if (licenseExpiring)
                    await NotifyDriverAsync(connection, notifications, tenantId, d.UserId.Value, d.DriverId,
                        "driver_license_expiring",
                        "License expiring soon",
                        $"Your driving license expires on {d.LicenseExpiry:yyyy-MM-dd}. Upload renewal in the driver app.",
                        cancellationToken);

                if (licenseExpired)
                    await NotifyDriverAsync(connection, notifications, tenantId, d.UserId.Value, d.DriverId,
                        "driver_license_expired",
                        "License expired",
                        "Your license has expired. Trip assignment is blocked until you renew and admin re-verifies.",
                        cancellationToken);

                if (rejected)
                    await NotifyDriverAsync(connection, notifications, tenantId, d.UserId.Value, d.DriverId,
                        "driver_verification_rejected",
                        "Verification rejected",
                        "Your verification was rejected. Check documents and contact fleet admin.",
                        cancellationToken);
            }

            await ScanComplianceDocExpiryAsync(connection, notifications, tenantId, threshold, cancellationToken);
        }
    }

    private static async Task ScanComplianceDocExpiryAsync(
        System.Data.IDbConnection connection,
        INotificationService notifications,
        int tenantId,
        DateTime threshold,
        CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(int DriverId, int? UserId, string DocType, DateTime Expiry)>(
            new CommandDefinition("""
                SELECT d.EntityId AS DriverId, dr.UserId, d.DocumentType, d.ExpiryDate
                FROM ComplianceDocuments d
                INNER JOIN Drivers dr ON dr.Id = d.EntityId AND dr.TenantId = @TenantId AND dr.IsDeleted = 0
                WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.EntityType = N'Driver'
                  AND d.ExpiryDate IS NOT NULL AND d.ExpiryDate <= @Threshold
                  AND dr.UserId IS NOT NULL
                """,
                new { TenantId = tenantId, Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            if (!row.UserId.HasValue) continue;
            var expired = row.Expiry.Date < DateTime.UtcNow.Date;
            var key = $"driver-doc-{row.DriverId}-{row.DocType}-{row.Expiry:yyyyMMdd}";
            await NotifyDriverAsync(
                connection, notifications, tenantId, row.UserId.Value, row.DriverId,
                key,
                expired ? $"{row.DocType} expired" : $"{row.DocType} expiring soon",
                expired
                    ? $"Your {row.DocType} expired on {row.Expiry:yyyy-MM-dd}. Upload a new document."
                    : $"Your {row.DocType} expires on {row.Expiry:yyyy-MM-dd}.",
                cancellationToken);
        }
    }

    private static async Task NotifyDriverAsync(
        System.Data.IDbConnection connection,
        INotificationService notifications,
        int tenantId,
        int userId,
        int driverId,
        string templateKey,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Notifications
            WHERE TenantId = @TenantId AND UserId = @UserId AND Module = N'driver_verification'
              AND TemplateKey = @Key AND IsDeleted = 0
              AND CreatedAt > DATEADD(DAY, -3, SYSUTCDATETIME())
            """, new { TenantId = tenantId, UserId = userId, Key = templateKey },
            cancellationToken: cancellationToken));
        if (exists > 0)
            return;

        await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
            UserId: userId,
            TenantId: tenantId,
            Title: title,
            Message: message,
            Type: NotificationType.TripUpdated,
            ReferenceId: driverId,
            Priority: 2,
            Module: "driver_verification",
            TemplateKey: templateKey,
            SendNow: true,
            Channels: ["InApp", "Push"]), cancellationToken);
    }
}
