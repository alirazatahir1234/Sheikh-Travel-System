using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Infrastructure.Services;

/// <summary>
/// Scans maintenance schedules and compliance documents to emit dashboard alerts.
/// </summary>
public class MaintenanceAlertHostedService(
    IServiceProvider serviceProvider,
    ILogger<MaintenanceAlertHostedService> logger) : BackgroundService
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
                    logger.LogError(ex, "Maintenance alert scan failed.");
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
        using var connection = dbFactory.CreateConnection();

        var tenants = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT DISTINCT TenantId FROM Vehicles WHERE TenantId IS NOT NULL AND IsDeleted = 0",
            cancellationToken: cancellationToken));

        foreach (var tenantId in tenants)
        {
            await ScanSchedulesAsync(connection, tenantId, cancellationToken);
            await ScanComplianceAsync(connection, tenantId, cancellationToken);
        }
    }

    private static async Task ScanSchedulesAsync(System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        var schedules = await connection.QueryAsync<ScheduleListRow>(new CommandDefinition(
            $"""
            SELECT {MaintenanceSql.ScheduleListSelect}
            {MaintenanceSql.ScheduleListFrom}
            WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.IsActive = 1
            """, new { TenantId = tenantId }, cancellationToken: ct));

        var today = DateTime.UtcNow.Date;

        foreach (var row in schedules)
        {
            var status = MaintenanceScheduleHelper.ComputeStatus(
                row.IntervalType, row.DueDate, row.NextServiceMileage, row.NextDueEngineHours,
                row.CurrentMileage, row.CurrentEngineHours, today);

            if (status is not (MaintenanceScheduleHelper.StatusDueSoon or MaintenanceScheduleHelper.StatusOverdue))
                continue;

            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM MaintenanceAlerts
                WHERE TenantId = @TenantId AND VehicleId = @VehicleId AND AlertType = N'ServiceDue'
                  AND ReferenceType = N'Schedule' AND IsDismissed = 0
                  AND CreatedAt > DATEADD(DAY, -7, GETUTCDATE())
                """, new { TenantId = tenantId, row.VehicleId }, cancellationToken: ct));

            if (exists > 0) continue;

            var overdue = status == MaintenanceScheduleHelper.StatusOverdue;
            var severity = overdue ? "Critical" : "Warning";
            var title = overdue ? "Service overdue" : "Service due soon";
            var detail = MaintenanceScheduleHelper.IsMileageInterval(row.IntervalType) && row.NextServiceMileage.HasValue
                ? $"{row.ServiceTypeName} for {row.VehicleName} due at {row.NextServiceMileage:N0} km (current {row.CurrentMileage:N0} km)"
                : MaintenanceScheduleHelper.IsEngineHoursInterval(row.IntervalType) && row.NextDueEngineHours.HasValue
                    ? $"{row.ServiceTypeName} for {row.VehicleName} due at {row.NextDueEngineHours:N0} hrs"
                    : $"{row.ServiceTypeName} for {row.VehicleName} due {(row.DueDate?.ToString("yyyy-MM-dd") ?? "soon")}";

            await MaintenanceAlertHelper.InsertAlertAsync(
                connection, tenantId, row.VehicleId, "ServiceDue", severity, title,
                detail, "Schedule", row.Id, ct);
        }
    }

    private static async Task ScanComplianceAsync(System.Data.IDbConnection connection, int tenantId, CancellationToken ct)
    {
        var thresholds = new[] { (30, "Warning"), (15, "Warning"), (7, "Critical"), (0, "Critical") };

        foreach (var (days, severity) in thresholds)
        {
            var docs = await connection.QueryAsync<(int EntityId, string EntityType, string DocType, DateTime Expiry, string? VehicleName)>(
                new CommandDefinition("""
                    SELECT d.EntityId, d.EntityType, d.DocumentType, d.ExpiryDate,
                           CASE WHEN d.EntityType = N'Vehicle' THEN v.Name ELSE NULL END
                    FROM ComplianceDocuments d
                    LEFT JOIN Vehicles v ON d.EntityType = N'Vehicle' AND v.Id = d.EntityId
                    WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.ExpiryDate IS NOT NULL
                      AND d.EntityType = N'Vehicle'
                      AND d.ExpiryDate <= DATEADD(DAY, @Days, GETUTCDATE())
                      AND d.ExpiryDate > DATEADD(DAY, @Days - 1, GETUTCDATE())
                    """, new { TenantId = tenantId, Days = days }, cancellationToken: ct));

            foreach (var doc in docs)
            {
                var title = days == 0 ? $"{doc.DocType} expired" : $"{doc.DocType} expiring in {days} days";
                await MaintenanceAlertHelper.InsertAlertAsync(
                    connection, tenantId, doc.EntityId, "Compliance", severity, title,
                    $"{doc.DocType} for vehicle {doc.VehicleName ?? doc.EntityId.ToString()} expires {doc.Expiry:yyyy-MM-dd}",
                    "ComplianceDocument", null, ct);
            }
        }

        var expired = await connection.QueryAsync<(int EntityId, string DocType, string? VehicleName, DateTime Expiry)>(
            new CommandDefinition("""
                SELECT d.EntityId, d.DocumentType, v.Name, d.ExpiryDate
                FROM ComplianceDocuments d
                LEFT JOIN Vehicles v ON v.Id = d.EntityId
                WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND d.EntityType = N'Vehicle'
                  AND d.ExpiryDate < GETUTCDATE()
                """, new { TenantId = tenantId }, cancellationToken: ct));

        foreach (var doc in expired)
        {
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM MaintenanceAlerts
                WHERE TenantId = @TenantId AND VehicleId = @VehicleId AND AlertType = N'Compliance'
                  AND Title LIKE @Title AND IsDismissed = 0
                """, new { TenantId = tenantId, VehicleId = doc.EntityId, Title = $"%{doc.DocType} expired%" },
                cancellationToken: ct));

            if (exists > 0) continue;

            await MaintenanceAlertHelper.InsertAlertAsync(
                connection, tenantId, doc.EntityId, "Compliance", "Critical",
                $"{doc.DocType} expired",
                $"{doc.DocType} for {doc.VehicleName ?? doc.EntityId.ToString()} expired on {doc.Expiry:yyyy-MM-dd}",
                "ComplianceDocument", null, ct);
        }
    }
}
