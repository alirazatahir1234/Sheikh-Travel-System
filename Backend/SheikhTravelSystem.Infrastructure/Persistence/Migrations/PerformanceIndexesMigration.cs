using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds indexes for high-frequency fleet, GPS, dashboard, and settings queries.
/// </summary>
public static class PerformanceIndexesMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsDevices_LastSeenAt' AND object_id = OBJECT_ID('GpsDevices'))
                    CREATE INDEX IX_GpsDevices_LastSeenAt ON GpsDevices (LastSeenAt DESC) WHERE IsDeleted = 0;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsDevices_VehicleId' AND object_id = OBJECT_ID('GpsDevices'))
                    CREATE INDEX IX_GpsDevices_VehicleId ON GpsDevices (VehicleId) WHERE IsDeleted = 0;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsDevices_TraccarDeviceId' AND object_id = OBJECT_ID('GpsDevices'))
                    CREATE INDEX IX_GpsDevices_TraccarDeviceId ON GpsDevices (TraccarDeviceId) WHERE IsDeleted = 0 AND TraccarDeviceId IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleTracking_VehicleId_Timestamp' AND object_id = OBJECT_ID('VehicleTracking'))
                    CREATE INDEX IX_VehicleTracking_VehicleId_Timestamp ON VehicleTracking (VehicleId, Timestamp DESC);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bookings_Status_IsDeleted' AND object_id = OBJECT_ID('Bookings'))
                    CREATE INDEX IX_Bookings_Status_IsDeleted ON Bookings (Status, IsDeleted) INCLUDE (TenantId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payments_Status_IsDeleted' AND object_id = OBJECT_ID('Payments'))
                    CREATE INDEX IX_Payments_Status_IsDeleted ON Payments (Status, IsDeleted);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Vehicles_IsDeleted_TenantId' AND object_id = OBJECT_ID('Vehicles'))
                    CREATE INDEX IX_Vehicles_IsDeleted_TenantId ON Vehicles (IsDeleted, TenantId) INCLUDE (Status);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FuelLogs_IsDeleted' AND object_id = OBJECT_ID('FuelLogs'))
                    CREATE INDEX IX_FuelLogs_IsDeleted ON FuelLogs (IsDeleted) INCLUDE (TotalCost);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Maintenance_IsDeleted' AND object_id = OBJECT_ID('Maintenance'))
                    CREATE INDEX IX_Maintenance_IsDeleted ON Maintenance (IsDeleted) INCLUDE (Cost);
                """, cancellationToken: cancellationToken));

            logger.LogInformation("PerformanceIndexesMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "PerformanceIndexesMigration failed.");
            throw;
        }
    }
}
