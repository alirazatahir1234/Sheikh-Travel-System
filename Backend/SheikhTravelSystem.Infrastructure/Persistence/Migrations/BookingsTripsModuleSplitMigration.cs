using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Splits bookings/trips out of TRAVEL so companies can enable Routes without Bookings &amp; Trips.
/// Backfills BOOKINGS onto tenants that already had TRAVEL or legacy bookings/trips keys.
/// </summary>
public static class BookingsTripsModuleSplitMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID('Modules', 'U') IS NULL RETURN;

            -- TRAVEL = roads/routes only
            UPDATE Modules SET
                ModuleName = N'Routes',
                DisplayName = N'Routes',
                Description = N'Roads and route catalog (independent of bookings/trips)',
                Icon = N'alt_route',
                Route = N'/routes',
                SortOrder = 50,
                DependenciesJson = N'["DASHBOARD"]',
                Status = N'Active',
                LegacyKeysJson = N'["routes"]'
            WHERE ModuleCode = N'TRAVEL';

            -- BOOKINGS = bookings + trips (enableable)
            IF EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = N'BOOKINGS')
                UPDATE Modules SET
                    ModuleName = N'Bookings & Trips',
                    DisplayName = N'Bookings & Trips',
                    Description = N'Booking and trip workflows — enable per company',
                    Category = N'Travel',
                    Icon = N'event_note',
                    Route = N'/bookings',
                    SortOrder = 51,
                    DependenciesJson = N'["DASHBOARD"]',
                    Visible = 1,
                    Status = N'Active',
                    LegacyKeysJson = N'["bookings","trips"]'
                WHERE ModuleCode = N'BOOKINGS';
            ELSE
                INSERT INTO Modules (
                    ModuleCode, ModuleName, DisplayName, Description, Category,
                    Version, Icon, Route, SortOrder, DependenciesJson, Visible,
                    IsMobileSupported, IsAISupported, IsGPSSupported, Status, LegacyKeysJson)
                VALUES (
                    N'BOOKINGS', N'Bookings & Trips', N'Bookings & Trips',
                    N'Booking and trip workflows — enable per company', N'Travel',
                    N'1.0.0', N'event_note', N'/bookings', 51, N'["DASHBOARD"]', 1,
                    1, 0, 0, N'Active', N'["bookings","trips"]');

            -- Catalog-only TRIPS stays under BOOKINGS parent
            UPDATE Modules SET
                Description = N'Trip lifecycle and dispatch (included in Bookings & Trips)',
                DependenciesJson = N'["BOOKINGS"]',
                Status = N'Active',
                LegacyKeysJson = NULL
            WHERE ModuleCode = N'TRIPS';
            """, cancellationToken: cancellationToken));

        if (await TableExistsAsync(connection, "TenantModules", cancellationToken))
        {
            // Tenants that already had TRAVEL previously also had bookings/trips — preserve entitlement.
            var backfilledFromTravel = await connection.ExecuteAsync(new CommandDefinition("""
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

            // Tenants with legacy JSON containing bookings/trips but no TenantModules BOOKINGS row.
            var tenantsWithLegacy = (await connection.QueryAsync<(int TenantId, string? Json)>(new CommandDefinition("""
                SELECT Id AS TenantId, EnabledModulesJson AS Json
                FROM Tenants
                WHERE EnabledModulesJson IS NOT NULL AND LTRIM(RTRIM(EnabledModulesJson)) <> ''
                """, cancellationToken: cancellationToken))).ToList();

            var bookingsModuleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Id FROM Modules WHERE ModuleCode = N'BOOKINGS'",
                cancellationToken: cancellationToken));

            var backfilledFromJson = 0;
            if (bookingsModuleId is int bookId)
            {
                foreach (var (tenantId, json) in tenantsWithLegacy)
                {
                    if (!LegacyJsonHasBookingsOrTrips(json))
                        continue;

                    var inserted = await connection.ExecuteAsync(new CommandDefinition("""
                        IF NOT EXISTS (
                            SELECT 1 FROM TenantModules
                            WHERE TenantId = @TenantId AND ModuleId = @ModuleId)
                            INSERT INTO TenantModules (TenantId, ModuleId) VALUES (@TenantId, @ModuleId);
                        """,
                        new { TenantId = tenantId, ModuleId = bookId },
                        cancellationToken: cancellationToken));
                    backfilledFromJson += inserted;
                }
            }

            // Re-sync EnabledModulesJson from TenantModules so TRAVEL no longer implies bookings/trips.
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
                    SET EnabledModulesJson = @Json, UpdatedAt = GETUTCDATE()
                    WHERE Id = @TenantId
                    """, new { TenantId = tenantId, Json = serialized }, cancellationToken: cancellationToken));
            }

            logger.LogInformation(
                "BookingsTripsModuleSplitMigration applied (TRAVEL→routes, BOOKINGS enableable). Backfill travel={Travel}, json={Json}, synced={Synced}.",
                backfilledFromTravel, backfilledFromJson, tenantIds.Count);
        }
        else
        {
            logger.LogInformation(
                "BookingsTripsModuleSplitMigration applied (Modules metadata only; TenantModules missing).");
        }
    }

    private static bool LegacyJsonHasBookingsOrTrips(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var keys = JsonSerializer.Deserialize<string[]>(json) ?? [];
            return keys.Any(k =>
                string.Equals(k, "bookings", StringComparison.OrdinalIgnoreCase)
                || string.Equals(k, "trips", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return json.Contains("bookings", StringComparison.OrdinalIgnoreCase)
                || json.Contains("trips", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<bool> TableExistsAsync(
        IDbConnection connection, string table, CancellationToken ct)
    {
        var n = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table)
            THEN 1 ELSE 0 END
            """, new { Table = table }, cancellationToken: ct));
        return n == 1;
    }
}
