using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsFleetStatusHistoryMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GpsFleetStatusSnapshots')
                CREATE TABLE GpsFleetStatusSnapshots (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    TenantId INT NOT NULL,
                    SnapshotAt DATETIME2 NOT NULL,
                    TotalVehicles INT NOT NULL,
                    Online INT NOT NULL,
                    Offline INT NOT NULL,
                    Moving INT NOT NULL,
                    Idle INT NOT NULL,
                    Parked INT NOT NULL,
                    NeverSeen INT NOT NULL,
                    Sos INT NOT NULL,
                    AlertsToday INT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
                """, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GpsFleetStatusSnapshots_Tenant_Time')
                    CREATE INDEX IX_GpsFleetStatusSnapshots_Tenant_Time ON GpsFleetStatusSnapshots(TenantId, SnapshotAt DESC);
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsFleetStatusHistoryMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsFleetStatusHistoryMigration failed.");
        }
    }
}
