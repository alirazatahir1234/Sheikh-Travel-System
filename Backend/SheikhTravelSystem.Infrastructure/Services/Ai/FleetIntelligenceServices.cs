using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

public sealed class FleetHealthService(IDbConnectionFactory dbFactory) : IFleetHealthService
{
    public async Task<FleetHealthDto> ComputeAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var gpsOnline = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT CASE WHEN COUNT(*) = 0 THEN 100
                ELSE CAST(100.0 * SUM(CASE WHEN DATEDIFF(MINUTE, vcl.LastUpdate, GETUTCDATE()) <= 15 THEN 1 ELSE 0 END)
                     / COUNT(*) AS DECIMAL(5,2)) END
            FROM Vehicles v
            LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var maintenance = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT CASE WHEN COUNT(*) = 0 THEN 100
                ELSE CAST(100.0 * SUM(CASE WHEN NextDueDate IS NULL OR NextDueDate > DATEADD(DAY, 7, GETUTCDATE()) THEN 1 ELSE 0 END)
                     / COUNT(*) AS DECIMAL(5,2)) END
            FROM Maintenance WHERE IsDeleted = 0
            """, cancellationToken: cancellationToken));

        var compliance = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition("""
            SELECT CASE WHEN COUNT(*) = 0 THEN 100
                ELSE CAST(100.0 * SUM(CASE WHEN ExpiryDate IS NULL OR ExpiryDate > DATEADD(DAY, 30, GETUTCDATE()) THEN 1 ELSE 0 END)
                     / COUNT(*) AS DECIMAL(5,2)) END
            FROM ComplianceDocuments WHERE IsDeleted = 0 AND TenantId = @TenantId
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var critical = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM GpsAlertEvents
            WHERE IsDeleted = 0 AND IsAcknowledged = 0
              AND Severity = 'critical'
              AND Timestamp > DATEADD(DAY, -1, GETUTCDATE())
            """, cancellationToken: cancellationToken));

        var driverScore = 80m;
        try
        {
            // Prefer recent overspeed pressure as inverse proxy when dedicated score table is absent.
            var overspeedPressure = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*) FROM GpsAlertEvents
                WHERE IsDeleted = 0 AND EventType = 'speed_exceeded'
                  AND Timestamp > DATEADD(DAY, -7, GETUTCDATE())
                """, cancellationToken: cancellationToken));
            driverScore = Math.Clamp(100 - overspeedPressure * 2, 40, 100);
        }
        catch { /* keep default */ }

        var criticalPenalty = Math.Min(30m, critical * 5m);
        var health = Math.Round(
            (gpsOnline * 0.30m) + (maintenance * 0.25m) + (compliance * 0.20m) + (driverScore * 0.25m) - criticalPenalty,
            2);
        health = Math.Clamp(health, 0, 100);

        var details = JsonSerializer.Serialize(new
        {
            gpsOnline,
            maintenance,
            compliance,
            driverScore,
            critical
        });

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO FleetHealthSnapshots
                (TenantId, HealthPercent, GpsOnlineRate, MaintenanceScore, ComplianceScore, DriverScore, CriticalAlerts, DetailsJson, CreatedAt)
            VALUES
                (@TenantId, @Health, @Gps, @Maint, @Comp, @Driver, @Critical, @Details, GETUTCDATE())
            """,
            new
            {
                TenantId = tenantId,
                Health = health,
                Gps = gpsOnline,
                Maint = maintenance,
                Comp = compliance,
                Driver = driverScore,
                Critical = critical,
                Details = details
            },
            cancellationToken: cancellationToken));

        return new FleetHealthDto(
            health, gpsOnline, maintenance, compliance, driverScore, critical,
            $"Fleet health {health:0.#}% — {critical} critical alert(s) open.");
    }
}

public sealed class AiDigestService(
    IDbConnectionFactory dbFactory,
    INotificationService notifications,
    IAiManagementService aiManagement,
    ILogger<AiDigestService> logger) : IAiDigestService
{
    public async Task GenerateMorningDigestAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var config = await aiManagement.GetConfigAsync(tenantId, cancellationToken);
        if (!config.DigestEnabled) return;

        using var connection = dbFactory.CreateConnection();
        var offline = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM VehicleCurrentLocation vcl
            INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.IsDeleted = 0 AND v.TenantId = @TenantId
            WHERE vcl.LastUpdate < DATEADD(MINUTE, -30, GETUTCDATE())
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        var maintenanceDue = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Maintenance
            WHERE IsDeleted = 0 AND NextDueDate IS NOT NULL AND NextDueDate <= DATEADD(DAY, 7, GETUTCDATE())
            """, cancellationToken: cancellationToken));

        var overspeed = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM GpsAlertEvents
            WHERE IsDeleted = 0 AND EventType = 'speed_exceeded'
              AND Timestamp >= CAST(GETUTCDATE() AS DATE)
            """, cancellationToken: cancellationToken));

        var critical = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM GpsAlertEvents
            WHERE IsDeleted = 0 AND IsAcknowledged = 0 AND Severity = 'critical'
            """, cancellationToken: cancellationToken));

        var title = "Morning Fleet Summary";
        var body =
            $"• {offline} vehicles offline\n" +
            $"• {maintenanceDue} maintenance due (7 days)\n" +
            $"• {overspeed} overspeed events today\n" +
            $"• {critical} critical alerts open";

        var statsJson = JsonSerializer.Serialize(new { offline, maintenanceDue, overspeed, critical });

        var digestId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO AiDigests (TenantId, DigestType, Title, Body, StatsJson, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@TenantId, 'Morning', @Title, @Body, @StatsJson, GETUTCDATE())
            """,
            new { TenantId = tenantId, Title = title, Body = body, StatsJson = statsJson },
            cancellationToken: cancellationToken));

        await notifications.CreateForAllChannelsAsync(
            title, body, NotificationType.TripDelayed,
            [NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email],
            priority: 2, module: "Fleet", referenceId: digestId, templateKey: null,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE AiDigests SET SentAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = digestId }, cancellationToken: cancellationToken));

        logger.LogInformation("Morning digest {DigestId} generated for tenant {TenantId}", digestId, tenantId);
    }
}

public sealed class AiRecommendationService(IDbConnectionFactory dbFactory) : IAiRecommendationService
{
    public async Task RefreshAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE AiRecommendations SET IsDismissed = 1
            WHERE TenantId = @TenantId AND Source = 'Rule' AND IsDismissed = 0
              AND CreatedAt < DATEADD(DAY, -1, GETUTCDATE())
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        // Low battery vehicles
        var lowBattery = await connection.QueryAsync<(int VehicleId, decimal? Battery)>(new CommandDefinition("""
            SELECT TOP 20 v.Id AS VehicleId, vcl.BatteryLevel AS Battery
            FROM Vehicles v
            INNER JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
            WHERE v.IsDeleted = 0 AND v.TenantId = @TenantId AND vcl.BatteryLevel IS NOT NULL AND vcl.BatteryLevel < 20
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        foreach (var v in lowBattery)
        {
            await UpsertRecAsync(connection, tenantId, "Vehicle", v.VehicleId, "Battery", "High",
                "Check battery", $"Vehicle #{v.VehicleId} battery at {v.Battery:0.#}%. Inspect charging / replace battery.",
                cancellationToken);
        }

        var overdue = await connection.QueryAsync<(int Id, int VehicleId)>(new CommandDefinition("""
            SELECT TOP 20 Id, VehicleId FROM Maintenance
            WHERE IsDeleted = 0 AND NextDueDate IS NOT NULL AND NextDueDate < GETUTCDATE()
            """, cancellationToken: cancellationToken));

        foreach (var m in overdue)
        {
            await UpsertRecAsync(connection, tenantId, "Vehicle", m.VehicleId, "Maintenance", "High",
                "Maintenance overdue", $"Schedule service for vehicle #{m.VehicleId} (maintenance #{m.Id}).",
                cancellationToken);
        }

        var compliance = await connection.QueryAsync<(int EntityId, string DocumentType, DateTime Expiry)>(new CommandDefinition("""
            SELECT TOP 20 EntityId, DocumentType, ExpiryDate
            FROM ComplianceDocuments
            WHERE IsDeleted = 0 AND TenantId = @TenantId AND ExpiryDate IS NOT NULL
              AND ExpiryDate <= DATEADD(DAY, 14, GETUTCDATE())
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        foreach (var c in compliance)
        {
            await UpsertRecAsync(connection, tenantId, "Vehicle", c.EntityId, "Compliance", "Medium",
                "Document expiring", $"{c.DocumentType} expires on {c.Expiry:yyyy-MM-dd}. Renew soon.",
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<AiRecommendationDto>> GetActiveAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<AiRecommendationDto>(new CommandDefinition("""
            SELECT TOP 50 Id, EntityType, EntityId, Category, Severity, Title, Action, Source, Score, CreatedAt
            FROM AiRecommendations
            WHERE TenantId = @TenantId AND IsDismissed = 0
              AND (ExpiresAt IS NULL OR ExpiresAt > GETUTCDATE())
            ORDER BY CASE Severity WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END, CreatedAt DESC
            """, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private static async Task UpsertRecAsync(
        System.Data.IDbConnection connection, int tenantId, string entityType, int entityId,
        string category, string severity, string title, string action, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM AiRecommendations
                WHERE TenantId = @TenantId AND EntityType = @EntityType AND EntityId = @EntityId
                  AND Category = @Category AND IsDismissed = 0 AND CreatedAt > DATEADD(DAY, -1, GETUTCDATE()))
            INSERT INTO AiRecommendations
                (TenantId, EntityType, EntityId, Category, Severity, Title, Action, Source, CreatedAt)
            VALUES (@TenantId, @EntityType, @EntityId, @Category, @Severity, @Title, @Action, 'Rule', GETUTCDATE())
            """,
            new { TenantId = tenantId, EntityType = entityType, EntityId = entityId, Category = category, Severity = severity, Title = title, Action = action },
            cancellationToken: ct));
    }
}
