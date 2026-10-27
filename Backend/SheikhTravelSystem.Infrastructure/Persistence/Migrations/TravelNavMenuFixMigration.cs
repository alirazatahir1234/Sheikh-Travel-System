using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Aligns travel sidebar rows with Angular routes (/bookings, /trips, /routes) and
/// backfills BOOKINGS entitlement for tenants that have TRAVEL (routes) only.
/// </summary>
public static class TravelNavMenuFixMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (await TableExistsAsync(connection, "PlatformMenus", cancellationToken))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE PlatformMenus
                SET Route = N'/trips',
                    PermissionCode = COALESCE(NULLIF(LTRIM(RTRIM(PermissionCode)), N''), N'Trip.View'),
                    ModuleKey = N'BOOKINGS',
                    DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), N''), N'Trips'),
                    IsActive = 1,
                    Visible = 1,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE IsActive = 1
                  AND (
                        PermissionCode = N'Trip.View'
                     OR Name = N'Trips'
                     OR DisplayName = N'Trips'
                  )
                  AND Route <> N'/trips';

                UPDATE PlatformMenus
                SET Route = N'/routes',
                    PermissionCode = COALESCE(NULLIF(LTRIM(RTRIM(PermissionCode)), N''), N'Route.View'),
                    ModuleKey = COALESCE(NULLIF(LTRIM(RTRIM(ModuleKey)), N''), N'TRAVEL'),
                    DisplayName = COALESCE(
                        NULLIF(LTRIM(RTRIM(DisplayName)), N''),
                        CASE WHEN Name IN (N'Roads', N'Routes') THEN Name ELSE N'Routes' END),
                    IsActive = 1,
                    Visible = 1,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE IsActive = 1
                  AND (
                        PermissionCode = N'Route.View'
                     OR Name IN (N'Routes', N'Roads')
                     OR DisplayName IN (N'Routes', N'Roads')
                     OR Route IN (N'/roads', N'/travel/routes')
                  )
                  AND Route <> N'/routes';

                UPDATE PlatformMenus
                SET Route = N'/bookings',
                    PermissionCode = COALESCE(NULLIF(LTRIM(RTRIM(PermissionCode)), N''), N'Booking.View'),
                    ModuleKey = N'BOOKINGS',
                    DisplayName = COALESCE(NULLIF(LTRIM(RTRIM(DisplayName)), N''), N'Bookings'),
                    IsActive = 1,
                    Visible = 1,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE IsActive = 1
                  AND (
                        PermissionCode = N'Booking.View'
                     OR Name = N'Bookings'
                     OR DisplayName = N'Bookings'
                  )
                  AND Route NOT IN (N'/bookings', N'/bookings/new');
                """, cancellationToken: cancellationToken));
        }

        if (await TableExistsAsync(connection, "TenantModules", cancellationToken)
            && await TableExistsAsync(connection, "Modules", cancellationToken))
        {
            var backfilled = await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO TenantModules (TenantId, ModuleId)
                SELECT tm.TenantId, book.Id
                FROM TenantModules tm
                INNER JOIN Modules travel ON travel.Id = tm.ModuleId AND travel.ModuleCode = N'TRAVEL'
                CROSS JOIN Modules book
                WHERE book.ModuleCode = N'BOOKINGS'
                  AND NOT EXISTS (
                      SELECT 1 FROM TenantModules x
                      WHERE x.TenantId = tm.TenantId AND x.ModuleId = book.Id);
                """, cancellationToken: cancellationToken));

            var tenantIds = (await connection.QueryAsync<int>(new CommandDefinition("""
                SELECT DISTINCT TenantId FROM TenantModules
                """, cancellationToken: cancellationToken))).ToList();

            foreach (var tenantId in tenantIds)
            {
                var codes = (await connection.QueryAsync<string>(new CommandDefinition("""
                    SELECT m.ModuleCode
                    FROM TenantModules tm
                    INNER JOIN Modules m ON m.Id = tm.ModuleId
                    WHERE tm.TenantId = @TenantId
                    """, new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();

                var legacyKeys = TenantModuleCatalog.LegacyKeysFromCodes(codes);
                var serialized = TenantModuleCatalog.SerializeLegacyKeys(legacyKeys);
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE Tenants
                    SET EnabledModulesJson = @Json, UpdatedAt = GETUTCDATETIME()
                    WHERE Id = @TenantId
                    """, new { TenantId = tenantId, Json = serialized }, cancellationToken: cancellationToken));
            }

            logger.LogInformation(
                "TravelNavMenuFixMigration applied (menu routes + BOOKINGS backfill for TRAVEL tenants, backfilled={Count}, synced={Synced}).",
                backfilled, tenantIds.Count);
        }
        else
        {
            logger.LogInformation("TravelNavMenuFixMigration applied (PlatformMenus only).");
        }
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.IDbConnection connection, string table, CancellationToken ct)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table)
            THEN 1 ELSE 0 END
            """, new { Table = table }, cancellationToken: ct));
        return n == 1;
    }
}
