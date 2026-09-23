using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Fixes Trips sidebar menu incorrectly seeded to /bookings (same screen as Bookings).
/// </summary>
public static class TripsMenuRouteFixMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus'
                ) THEN 1 ELSE 0 END
                """,
                cancellationToken: cancellationToken)) != 1)
        {
            return;
        }

        var updated = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE PlatformMenus
            SET Route = N'/trips',
                DisplayName = N'Trips',
                Icon = COALESCE(NULLIF(LTRIM(RTRIM(Icon)), N''), N'route'),
                PermissionCode = COALESCE(NULLIF(LTRIM(RTRIM(PermissionCode)), N''), N'Trip.View')
            WHERE IsActive = 1
              AND Route = N'/bookings'
              AND (
                    Name = N'Trips'
                 OR DisplayName IN (N'Trips', N'Bookings') AND PermissionCode = N'Trip.View'
                 OR (PermissionCode = N'Trip.View' AND Name <> N'Bookings')
              );
            """,
            cancellationToken: cancellationToken));

        logger.LogInformation(
            "TripsMenuRouteFixMigration applied — corrected {Count} menu row(s) from /bookings to /trips.",
            updated);
    }
}
