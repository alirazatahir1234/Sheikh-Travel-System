using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsDeviceCommandsMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('GpsDeviceCommands', 'Reason') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD Reason NVARCHAR(200) NULL;

                IF COL_LENGTH('GpsDeviceCommands', 'TenantId') IS NULL
                    ALTER TABLE GpsDeviceCommands ADD TenantId INT NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsDeviceCommandsMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsDeviceCommandsMigration failed.");
        }
    }
}
