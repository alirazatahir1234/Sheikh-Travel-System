using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Dedupes auto-created DriverViolations per GpsAlertId (telematics enforcement).
/// </summary>
public static class DriverTelematicsEnforcementMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'UQ_DriverViolations_GpsAlertId'
                      AND object_id = OBJECT_ID('DriverViolations'))
                BEGIN
                    CREATE UNIQUE INDEX UQ_DriverViolations_GpsAlertId
                        ON DriverViolations(GpsAlertId)
                        WHERE GpsAlertId IS NOT NULL AND IsDeleted = 0;
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("DriverTelematicsEnforcementMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DriverTelematicsEnforcementMigration failed.");
        }
    }
}
