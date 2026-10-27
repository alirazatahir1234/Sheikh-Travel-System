using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.MaintenanceModule;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Notifies assigned drivers when predictive maintenance flags their vehicle (push + in-app).
/// </summary>
public class PredictiveMaintenanceDriverNotifyHostedService(
    IServiceProvider serviceProvider,
    ILogger<PredictiveMaintenanceDriverNotifyHostedService> logger) : BackgroundService
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
                    logger.LogError(ex, "Predictive maintenance driver notification scan failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
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
        var maintenanceRepo = scope.ServiceProvider.GetRequiredService<IMaintenanceModuleRepository>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        using var connection = dbFactory.CreateConnection();
        var tenants = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT DISTINCT TenantId FROM Vehicles WHERE TenantId IS NOT NULL AND IsDeleted = 0",
            cancellationToken: cancellationToken));

        foreach (var tenantId in tenants)
        {
            var dashboard = await maintenanceRepo.GetMaintenanceDashboardAsync(
                new GetMaintenanceDashboardQuery(BranchId: null), cancellationToken);
            if (!dashboard.Success || dashboard.Data?.PredictiveAlerts is not { Count: > 0 } alerts)
                continue;

            foreach (var alert in alerts.Where(a =>
                         a.Severity is "Critical" or "Warning"
                         && a.Status is "DueSoon" or "Overdue"))
            {
                var driverUserId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
                    SELECT TOP 1 d.UserId
                    FROM AssignmentHistory ah
                    INNER JOIN Drivers d ON d.Id = ah.DriverId AND d.IsDeleted = 0
                    WHERE ah.TenantId = @TenantId AND ah.VehicleId = @VehicleId AND ah.IsDeleted = 0
                      AND ah.Status = N'Active' AND d.UserId IS NOT NULL
                    ORDER BY ah.StartAt DESC
                    """, new { TenantId = tenantId, alert.VehicleId }, cancellationToken: cancellationToken));

                if (!driverUserId.HasValue)
                    continue;

                var dedupeKey = $"pred-maint-{alert.VehicleId}-{alert.Category}-{alert.Status}";
                var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    SELECT COUNT(*) FROM Notifications
                    WHERE TenantId = @TenantId AND UserId = @UserId AND Module = N'maintenance'
                      AND TemplateKey = @Key AND IsDeleted = 0
                      AND CreatedAt > DATEADD(DAY, -3, GETUTCDATETIME())
                    """, new { TenantId = tenantId, UserId = driverUserId.Value, Key = dedupeKey },
                    cancellationToken: cancellationToken));
                if (exists > 0)
                    continue;

                await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                    UserId: driverUserId.Value,
                    TenantId: tenantId,
                    Title: alert.Title,
                    Message: alert.Message,
                    Type: NotificationType.TripUpdated,
                    ReferenceId: alert.VehicleId,
                    Priority: alert.Severity == "Critical" ? 1 : 2,
                    Module: "maintenance",
                    TemplateKey: dedupeKey,
                    SendNow: true,
                    Channels: ["InApp", "Push"]), cancellationToken);
            }
        }
    }
}
