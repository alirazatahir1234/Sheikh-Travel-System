using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsDeviceInstallationMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger)
    {
        try
        {
            using var connection = dbFactory.CreateConnection();
            await connection.ExecuteAsync(@"
                IF COL_LENGTH('GpsDevices', 'SerialNumber') IS NULL
                    ALTER TABLE GpsDevices ADD SerialNumber NVARCHAR(100) NULL;

                IF COL_LENGTH('GpsDevices', 'InstallationDate') IS NULL
                    ALTER TABLE GpsDevices ADD InstallationDate DATETIME2 NULL;

                IF COL_LENGTH('GpsDevices', 'InstalledBy') IS NULL
                    ALTER TABLE GpsDevices ADD InstalledBy NVARCHAR(200) NULL;

                IF COL_LENGTH('GpsDevices', 'InstallationNotes') IS NULL
                    ALTER TABLE GpsDevices ADD InstallationNotes NVARCHAR(500) NULL;

                IF COL_LENGTH('GpsDevices', 'RelayOutput') IS NULL
                    ALTER TABLE GpsDevices ADD RelayOutput NVARCHAR(50) NULL;
            ");
            logger.LogInformation("GpsDeviceInstallationMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsDeviceInstallationMigration failed.");
            throw;
        }
    }
}
