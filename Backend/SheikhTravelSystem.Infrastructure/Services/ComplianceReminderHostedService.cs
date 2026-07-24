using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services;

public class ComplianceReminderHostedService(
    IServiceProvider serviceProvider,
    ILogger<ComplianceReminderHostedService> logger) : BackgroundService
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
                    logger.LogError(ex, "Compliance reminder scan failed.");
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown when the host stops (e.g. duplicate dotnet run / port bind failure).
        }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var decisionEngine = scope.ServiceProvider.GetRequiredService<INotificationDecisionEngine>();

        using var connection = dbFactory.CreateConnection();
        var threshold = DateTime.UtcNow.AddDays(30);

        var expiringDrivers = await connection.QueryAsync<(int Id, string Name, DateTime Expiry, int TenantId)>(
            new CommandDefinition(
                @"SELECT Id, FullName, LicenseExpiryDate, TenantId FROM Drivers
                  WHERE IsDeleted = 0 AND IsActive = 1 AND LicenseExpiryDate <= @Threshold
                    AND TenantId IS NOT NULL AND TenantId > 0",
                new { Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var d in expiringDrivers)
        {
            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "compliance_reminder",
                "Driver license expiring",
                $"{d.Name} license expires on {d.Expiry:yyyy-MM-dd}.",
                NotificationType.TripDelayed,
                ReferenceId: d.Id,
                TenantId: d.TenantId,
                SuggestedPriority: 3,
                RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Email]), cancellationToken);
        }

        var expiringVehicles = await connection.QueryAsync<(int Id, string Name, DateTime? Expiry, int TenantId)>(
            new CommandDefinition(
                @"SELECT Id, Name, InsuranceExpiryDate, TenantId FROM Vehicles
                  WHERE IsDeleted = 0 AND InsuranceExpiryDate IS NOT NULL AND InsuranceExpiryDate <= @Threshold
                    AND TenantId IS NOT NULL AND TenantId > 0",
                new { Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var v in expiringVehicles)
        {
            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "compliance_reminder",
                "Vehicle insurance expiring",
                $"{v.Name} insurance expires on {v.Expiry:yyyy-MM-dd}.",
                NotificationType.VehicleOffline,
                ReferenceId: v.Id,
                TenantId: v.TenantId,
                SuggestedPriority: 3,
                RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Email]), cancellationToken);
        }

        var maintenanceDue = await connection.QueryAsync<(int Id, int VehicleId, DateTime Due, int TenantId)>(
            new CommandDefinition(
                @"SELECT m.Id, m.VehicleId, m.NextDueDate, v.TenantId
                  FROM Maintenance m
                  INNER JOIN Vehicles v ON v.Id = m.VehicleId
                  WHERE m.IsDeleted = 0 AND m.NextDueDate IS NOT NULL AND m.NextDueDate <= @Threshold
                    AND v.TenantId IS NOT NULL AND v.TenantId > 0",
                new { Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var m in maintenanceDue)
        {
            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "compliance_reminder",
                "Maintenance due",
                $"Vehicle #{m.VehicleId} maintenance due on {m.Due:yyyy-MM-dd}.",
                NotificationType.VehicleOffline,
                ReferenceId: m.VehicleId,
                TenantId: m.TenantId,
                SuggestedPriority: 3,
                RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Email]), cancellationToken);
        }

        var docExpiring = await connection.QueryAsync<(int VehicleId, string Type, DateTime Expiry, int TenantId)>(
            new CommandDefinition(
                @"SELECT d.VehicleId, d.DocumentType, d.ExpiryDate, v.TenantId
                  FROM VehicleDocuments d
                  INNER JOIN Vehicles v ON v.Id = d.VehicleId
                  WHERE d.IsDeleted = 0 AND d.ExpiryDate IS NOT NULL AND d.ExpiryDate <= @Threshold
                    AND v.TenantId IS NOT NULL AND v.TenantId > 0",
                new { Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var doc in docExpiring)
        {
            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "compliance_reminder",
                "Vehicle document expiring",
                $"{doc.Type} for vehicle #{doc.VehicleId} expires on {doc.Expiry:yyyy-MM-dd}.",
                NotificationType.VehicleOffline,
                ReferenceId: doc.VehicleId,
                TenantId: doc.TenantId,
                SuggestedPriority: 3,
                RequestedChannels: [NotificationChannels.InApp, NotificationChannels.Email]), cancellationToken);
        }
    }
}
