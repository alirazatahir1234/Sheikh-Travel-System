using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Indexes for Driver Management Operational / Risk stats and list COUNT paths.
/// Justified by GetStatsAsync / list queries joining latest active AssignmentHistory per driver
/// and aggregating GpsDevices.LastSeenAt for GPS Online.
/// </summary>
public static class DriverStatsPerformanceIndexesMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                -- Latest active assignment lookup: PARTITION BY DriverId ORDER BY StartAt DESC WHERE Status=Active
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AssignmentHistory_Driver_Active' AND object_id = OBJECT_ID('AssignmentHistory'))
                    CREATE INDEX IX_AssignmentHistory_Driver_Active
                    ON AssignmentHistory (DriverId, StartAt DESC)
                    INCLUDE (VehicleId, TenantId)
                    WHERE IsDeleted = 0 AND Status = N'Active' AND DriverId IS NOT NULL;

                -- Tenant-scoped driver roster scans for /drivers/stats and list COUNT
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Drivers_Tenant_Active' AND object_id = OBJECT_ID('Drivers'))
                    CREATE INDEX IX_Drivers_Tenant_Active
                    ON Drivers (TenantId, IsDeleted, BranchId)
                    INCLUDE (Status, IsActive, LicenseExpiryDate, VerificationStatus);

                -- OpenViolations join on scoped drivers
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DriverViolations_Tenant_Open' AND object_id = OBJECT_ID('DriverViolations'))
                    CREATE INDEX IX_DriverViolations_Tenant_Open
                    ON DriverViolations (TenantId, DriverId)
                    INCLUDE (Status)
                    WHERE IsDeleted = 0;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("DriverStatsPerformanceIndexesMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DriverStatsPerformanceIndexesMigration failed.");
            throw;
        }
    }
}
