using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Indexes for vehicle inventory list OUTER APPLY lookups (primary image, service due, active assignment).
/// Live schema had only PK on VehicleDocuments and no VehicleId index on Maintenance.
/// </summary>
public static class VehicleInventoryPerformanceIndexesMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                -- Vehicle list image OUTER APPLY: VehicleId + VehicleImage + CreatedAt (primary preference uses Notes)
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VehicleDocuments_Vehicle_Image' AND object_id = OBJECT_ID(N'VehicleDocuments'))
                    CREATE INDEX IX_VehicleDocuments_Vehicle_Image
                    ON VehicleDocuments (VehicleId, CreatedAt DESC)
                    INCLUDE (FileUrl, Notes)
                    WHERE IsDeleted = 0
                      AND DocumentType = N'VehicleImage'
                      AND FileUrl IS NOT NULL;

                -- Vehicle list service-due OUTER APPLY (schema SQL had IX_Maintenance_VehicleId but prod DB did not)
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Maintenance_VehicleId' AND object_id = OBJECT_ID(N'Maintenance'))
                    CREATE INDEX IX_Maintenance_VehicleId
                    ON Maintenance (VehicleId, NextDueDate ASC)
                    INCLUDE (NextDueMileage, Status)
                    WHERE IsDeleted = 0;

                -- Active assignment lookup per vehicle (list page Status = Active)
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AssignmentHistory_Vehicle_Active' AND object_id = OBJECT_ID(N'AssignmentHistory'))
                    CREATE INDEX IX_AssignmentHistory_Vehicle_Active
                    ON AssignmentHistory (VehicleId, StartAt DESC)
                    INCLUDE (DriverId, TenantId)
                    WHERE IsDeleted = 0 AND Status = N'Active';

                -- Tenant-scoped inventory ORDER BY CreatedAt + Status filter
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Vehicles_Tenant_Created' AND object_id = OBJECT_ID(N'Vehicles'))
                    CREATE INDEX IX_Vehicles_Tenant_Created
                    ON Vehicles (TenantId, CreatedAt DESC)
                    INCLUDE (Status, Name, RegistrationNumber, VehicleCode, Make, Model, Year, GpsDeviceId, BranchId)
                    WHERE IsDeleted = 0;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("VehicleInventoryPerformanceIndexesMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "VehicleInventoryPerformanceIndexesMigration failed.");
            throw;
        }
    }
}
