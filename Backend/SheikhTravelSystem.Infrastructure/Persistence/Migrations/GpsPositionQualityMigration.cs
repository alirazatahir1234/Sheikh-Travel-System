using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class GpsPositionQualityMigration
{
    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF COL_LENGTH('VehicleCurrentLocation', 'IsGpsValid') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD IsGpsValid BIT NOT NULL CONSTRAINT DF_VCL_IsGpsValid DEFAULT (1);
                IF COL_LENGTH('VehicleCurrentLocation', 'GpsQualityCode') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD GpsQualityCode NVARCHAR(32) NULL;
                IF COL_LENGTH('VehicleCurrentLocation', 'IdenticalCoordinateStreak') IS NULL
                    ALTER TABLE VehicleCurrentLocation ADD IdenticalCoordinateStreak INT NOT NULL CONSTRAINT DF_VCL_CoordStreak DEFAULT (0);

                IF COL_LENGTH('GpsPositions', 'IsGpsValid') IS NULL
                    ALTER TABLE GpsPositions ADD IsGpsValid BIT NOT NULL CONSTRAINT DF_GpsPos_IsGpsValid DEFAULT (1);
                IF COL_LENGTH('GpsPositions', 'GpsQualityCode') IS NULL
                    ALTER TABLE GpsPositions ADD GpsQualityCode NVARCHAR(32) NULL;
                """, cancellationToken: cancellationToken));

            logger.LogInformation("GpsPositionQualityMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GpsPositionQualityMigration failed.");
        }
    }
}
