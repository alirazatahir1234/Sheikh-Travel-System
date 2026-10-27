using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Per-tenant GPS trip creation mode (booking vs daily auto-trip) and GpsTrips source tagging.
/// </summary>
public static class GpsAutoTripModeMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('TenantGpsSettings', 'TripCreationMode') IS NULL
                ALTER TABLE TenantGpsSettings ADD TripCreationMode NVARCHAR(32) NOT NULL
                    CONSTRAINT DF_TenantGpsSettings_TripCreationMode DEFAULT 'Booking';

            IF COL_LENGTH('TenantGpsSettings', 'UpdatedAt') IS NULL
                ALTER TABLE TenantGpsSettings ADD UpdatedAt DATETIME2 NOT NULL
                    CONSTRAINT DF_TenantGpsSettings_UpdatedAt DEFAULT GETUTCDATE();

            IF COL_LENGTH('GpsTrips', 'DetectionSource') IS NULL
                ALTER TABLE GpsTrips ADD DetectionSource NVARCHAR(20) NOT NULL
                    CONSTRAINT DF_GpsTrips_DetectionSource DEFAULT 'Segment';

            IF COL_LENGTH('GpsTrips', 'LocalTripDate') IS NULL
                ALTER TABLE GpsTrips ADD LocalTripDate DATE NULL;
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_GpsTrips_Vehicle_LocalDaily')
                CREATE UNIQUE INDEX UX_GpsTrips_Vehicle_LocalDaily
                ON GpsTrips (VehicleId, LocalTripDate)
                WHERE DetectionSource = 'DailyAuto' AND LocalTripDate IS NOT NULL;
            """, cancellationToken: cancellationToken));

        logger.LogInformation("GpsAutoTripModeMigration applied.");
    }
}
