using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
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
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        using var connection = dbFactory.CreateConnection();
        var threshold = DateTime.UtcNow.AddDays(30);

        var expiringDrivers = await connection.QueryAsync<(int Id, string Name, DateTime Expiry)>(
            new CommandDefinition(
                @"SELECT Id, FullName, LicenseExpiryDate FROM Drivers
                  WHERE IsDeleted = 0 AND IsActive = 1 AND LicenseExpiryDate <= @Threshold",
                new { Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var d in expiringDrivers)
        {
            await notifications.CreateForAllAsync(
                "Driver license expiring",
                $"{d.Name} license expires on {d.Expiry:yyyy-MM-dd}.",
                NotificationType.TripDelayed,
                d.Id,
                cancellationToken);
        }

        var expiringVehicles = await connection.QueryAsync<(int Id, string Name, DateTime? Expiry)>(
            new CommandDefinition(
                @"SELECT Id, Name, InsuranceExpiryDate FROM Vehicles
                  WHERE IsDeleted = 0 AND InsuranceExpiryDate IS NOT NULL AND InsuranceExpiryDate <= @Threshold",
                new { Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var v in expiringVehicles)
        {
            await notifications.CreateForAllAsync(
                "Vehicle insurance expiring",
                $"{v.Name} insurance expires on {v.Expiry:yyyy-MM-dd}.",
                NotificationType.VehicleOffline,
                v.Id,
                cancellationToken);
        }

        var maintenanceDue = await connection.QueryAsync<(int Id, int VehicleId, DateTime Due)>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, NextDueDate FROM Maintenance
                  WHERE IsDeleted = 0 AND NextDueDate IS NOT NULL AND NextDueDate <= @Threshold",
                new { Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var m in maintenanceDue)
        {
            await notifications.CreateForAllAsync(
                "Maintenance due",
                $"Vehicle #{m.VehicleId} maintenance due on {m.Due:yyyy-MM-dd}.",
                NotificationType.VehicleOffline,
                m.VehicleId,
                cancellationToken);
        }

        var docExpiring = await connection.QueryAsync<(int VehicleId, string Type, DateTime Expiry)>(
            new CommandDefinition(
                @"SELECT VehicleId, DocumentType, ExpiryDate FROM VehicleDocuments
                  WHERE IsDeleted = 0 AND ExpiryDate IS NOT NULL AND ExpiryDate <= @Threshold",
                new { Threshold = threshold },
                cancellationToken: cancellationToken));

        foreach (var doc in docExpiring)
        {
            await notifications.CreateForAllAsync(
                "Vehicle document expiring",
                $"{doc.Type} for vehicle #{doc.VehicleId} expires on {doc.Expiry:yyyy-MM-dd}.",
                NotificationType.VehicleOffline,
                doc.VehicleId,
                cancellationToken);
        }
    }
}
