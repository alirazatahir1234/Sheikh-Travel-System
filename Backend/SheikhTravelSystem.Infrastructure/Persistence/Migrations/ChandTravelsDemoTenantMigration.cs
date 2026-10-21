using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Idempotent demo tenant "Chand Travels" (slug chand-travels) with standard system roles.
/// Users (OWNER + DRIVER) are seeded by <see cref="DatabaseSeeder"/> which has the password hasher.
/// Also purges any leftover mis-seeded HONOR role rows if present.
/// </summary>
public static class ChandTravelsDemoTenantMigration
{
    public const string TenantSlug = "chand-travels";
    public const string TenantName = "Chand Travels";

    private static readonly (string Name, string Code)[] RoleDefs =
    [
        ("Super Admin", "SUPER_ADMIN"),
        ("Tenant Admin", "TENANT_ADMIN"),
        ("Owner", "OWNER"),
        ("Fleet Manager", "FLEET_MANAGER"),
        ("Dispatcher", "DISPATCHER"),
        ("GPS Operator", "GPS_OPERATOR"),
        ("Driver Manager", "DRIVER_MANAGER"),
        ("Accountant", "ACCOUNTANT"),
        ("Driver", "DRIVER"),
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var hasTenants = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'Tenants', N'U') IS NOT NULL THEN 1 ELSE 0 END",
            cancellationToken: cancellationToken));
        if (hasTenants == 0) return;

        // Purge mis-heard HONOR role if a prior build inserted it.
        await PurgeHonorRoleAsync(connection, cancellationToken);

        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 Id FROM Tenants WHERE Slug = @Slug",
            new { Slug = TenantSlug },
            cancellationToken: cancellationToken));

        if (tenantId is null or 0)
        {
            var hasCode = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN COL_LENGTH('Tenants', 'Code') IS NOT NULL THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken)) == 1;

            if (hasCode)
            {
                tenantId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO Tenants (Name, Slug, Code, IsActive, EnabledModulesJson, CreatedAt)
                    VALUES (@Name, @Slug, N'CHAND_TRAVELS', 1,
                        N'["vehicles","drivers","gps-tracking","trips","fuel-logs","maintenance","reports","bookings"]',
                        GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """,
                    new { Name = TenantName, Slug = TenantSlug },
                    cancellationToken: cancellationToken));
            }
            else
            {
                tenantId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                    INSERT INTO Tenants (Name, Slug, IsActive, EnabledModulesJson, CreatedAt)
                    VALUES (@Name, @Slug, 1,
                        N'["vehicles","drivers","gps-tracking","trips","fuel-logs","maintenance","reports","bookings"]',
                        GETUTCDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """,
                    new { Name = TenantName, Slug = TenantSlug },
                    cancellationToken: cancellationToken));
            }

            logger.LogInformation(
                "ChandTravelsDemoTenantMigration created tenant {TenantName} (Id={TenantId}, Slug={Slug}).",
                TenantName, tenantId, TenantSlug);
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Tenants
                SET Name = @Name, IsActive = 1
                WHERE Id = @TenantId;
                """,
                new { Name = TenantName, TenantId = tenantId },
                cancellationToken: cancellationToken));
        }

        var resolvedTenantId = tenantId!.Value;

        foreach (var (name, code) in RoleDefs)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Roles WHERE TenantId = @TenantId AND Code = @Code)
                INSERT INTO Roles (TenantId, Name, Code, IsSystem, IsActive, CreatedAt)
                VALUES (@TenantId, @Name, @Code, 1, 1, GETUTCDATE());
                """,
                new { TenantId = resolvedTenantId, Name = name, Code = code },
                cancellationToken: cancellationToken));
        }

        await TenantRolePermissionSeeder.SeedSystemRolePermissionsForTenantAsync(
            connection, resolvedTenantId, cancellationToken);

        try
        {
            var hasBranding = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'TenantBranding', N'U') IS NOT NULL THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken));
            if (hasBranding == 1)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    IF NOT EXISTS (SELECT 1 FROM TenantBranding WHERE TenantId = @TenantId)
                    INSERT INTO TenantBranding (TenantId, Country, CurrencyCode, TimeZone)
                    VALUES (@TenantId, N'Pakistan', N'PKR', N'Asia/Karachi');
                    """,
                    new { TenantId = resolvedTenantId },
                    cancellationToken: cancellationToken));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Chand Travels branding seed skipped.");
        }

        logger.LogInformation(
            "ChandTravelsDemoTenantMigration applied for tenant Id={TenantId}.",
            resolvedTenantId);
    }

    private static async Task PurgeHonorRoleAsync(
        System.Data.IDbConnection connection,
        CancellationToken cancellationToken)
    {
        var hasRoles = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'Roles', N'U') IS NOT NULL THEN 1 ELSE 0 END",
            cancellationToken: cancellationToken));
        if (hasRoles == 0) return;

        var hasUserRoles = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'UserRoles', N'U') IS NOT NULL THEN 1 ELSE 0 END",
            cancellationToken: cancellationToken));
        var hasRolePermissions = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'RolePermissions', N'U') IS NOT NULL THEN 1 ELSE 0 END",
            cancellationToken: cancellationToken));

        if (hasUserRoles == 1)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                DELETE ur FROM UserRoles ur
                INNER JOIN Roles r ON r.Id = ur.RoleId
                WHERE r.Code = N'HONOR';
                """, cancellationToken: cancellationToken));
        }

        if (hasRolePermissions == 1)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                DELETE rp FROM RolePermissions rp
                INNER JOIN Roles r ON r.Id = rp.RoleId
                WHERE r.Code = N'HONOR';
                """, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Roles WHERE Code = N'HONOR';",
            cancellationToken: cancellationToken));
    }
}
