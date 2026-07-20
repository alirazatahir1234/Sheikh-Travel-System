using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Driver fuel receipts: store uploaded receipt image URL on FuelLogs.
/// </summary>
public static class DriverFuelReceiptMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'FuelLogs')
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                               WHERE TABLE_NAME = 'FuelLogs' AND COLUMN_NAME = 'ReceiptUrl')
                    ALTER TABLE FuelLogs ADD ReceiptUrl NVARCHAR(1000) NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FuelLogs_Driver_FuelDate')
                    CREATE INDEX IX_FuelLogs_Driver_FuelDate ON FuelLogs (DriverId, FuelDate DESC)
                    WHERE IsDeleted = 0 AND DriverId IS NOT NULL;
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("DriverFuelReceiptMigration applied.");
    }
}
