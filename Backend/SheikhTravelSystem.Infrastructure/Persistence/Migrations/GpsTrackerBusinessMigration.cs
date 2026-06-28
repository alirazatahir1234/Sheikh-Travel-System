using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsTrackerBusinessMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsDevices', 'Category') IS NULL
                    ALTER TABLE GpsDevices ADD Category NVARCHAR(50) NULL;

                IF COL_LENGTH('GpsDevices', 'Phone') IS NULL
                    ALTER TABLE GpsDevices ADD Phone NVARCHAR(30) NULL;

                IF COL_LENGTH('GpsDevices', 'Contact') IS NULL
                    ALTER TABLE GpsDevices ADD Contact NVARCHAR(200) NULL;

                IF COL_LENGTH('GpsDevices', 'Disabled') IS NULL
                    ALTER TABLE GpsDevices ADD Disabled BIT NOT NULL CONSTRAINT DF_GpsDevices_Disabled DEFAULT 0;

                IF COL_LENGTH('GpsDevices', 'DriverId') IS NULL
                    ALTER TABLE GpsDevices ADD DriverId INT NULL;

                IF COL_LENGTH('GpsDevices', 'TrackerModelKey') IS NULL
                    ALTER TABLE GpsDevices ADD TrackerModelKey NVARCHAR(50) NULL;

                IF COL_LENGTH('GpsDevices', 'CountryCode') IS NULL
                    ALTER TABLE GpsDevices ADD CountryCode NVARCHAR(10) NULL;

                IF COL_LENGTH('GpsDevices', 'CurrentStatus') IS NULL
                    ALTER TABLE GpsDevices ADD CurrentStatus NVARCHAR(30) NULL;

                IF COL_LENGTH('GpsDevices', 'SIMProvider') IS NULL
                    ALTER TABLE GpsDevices ADD SIMProvider NVARCHAR(100) NULL;

                IF COL_LENGTH('GpsDevices', 'SIMPackage') IS NULL
                    ALTER TABLE GpsDevices ADD SIMPackage NVARCHAR(50) NULL;

                IF COL_LENGTH('GpsDevices', 'MonthlySIMCost') IS NULL
                    ALTER TABLE GpsDevices ADD MonthlySIMCost DECIMAL(10,2) NULL;

                IF COL_LENGTH('GpsDevices', 'WarrantyStart') IS NULL
                    ALTER TABLE GpsDevices ADD WarrantyStart DATE NULL;

                IF COL_LENGTH('GpsDevices', 'WarrantyEnd') IS NULL
                    ALTER TABLE GpsDevices ADD WarrantyEnd DATE NULL;

                IF COL_LENGTH('GpsDevices', 'PurchaseDate') IS NULL
                    ALTER TABLE GpsDevices ADD PurchaseDate DATE NULL;

                IF COL_LENGTH('GpsDevices', 'PurchasePrice') IS NULL
                    ALTER TABLE GpsDevices ADD PurchasePrice DECIMAL(12,2) NULL;

                IF COL_LENGTH('GpsDevices', 'LastSyncAt') IS NULL
                    ALTER TABLE GpsDevices ADD LastSyncAt DATETIME2 NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsTrackerBusinessMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsTrackerBusinessMigration failed.");
            throw;
        }
    }
}
