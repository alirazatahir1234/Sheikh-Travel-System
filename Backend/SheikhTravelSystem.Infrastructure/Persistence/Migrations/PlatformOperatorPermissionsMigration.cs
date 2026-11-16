using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// SG-05: Seed Platform.Tenants.* operator permissions, grant SUPER_ADMIN the full catalog
/// in the platform tenant, create PLATFORM_AUDITOR, strip platform-scope grants outside
/// tenant 1, and sync TENANT_ADMIN templates so prefix shortcuts can be removed.
/// Idempotent — safe to re-run.
/// </summary>
public static class PlatformOperatorPermissionsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var newPermissions = new (string Module, string Code, string Desc)[]
        {
            ("Platform", PlatformPermissions.TenantsDataRead,
                "Cross-tenant act-as and platform data reads"),
            ("Platform", PlatformPermissions.TenantsAuditReadAll,
                "Read audit events across all companies"),
            ("Platform", PlatformPermissions.TenantsUsersReadAll,
                "Read users across all companies"),
            ("Platform", PlatformPermissions.TenantsImpersonate,
                "Impersonate a user (no endpoint yet)"),
        };

        foreach (var (module, code, desc) in newPermissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (@Module, @Code, @Desc);
                """, new { Module = module, Code = code, Desc = desc }, cancellationToken: cancellationToken));
        }

        // Remove unseeded / renamed Platform.Vehicles.ViewAll
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE rp
            FROM RolePermissions rp
            INNER JOIN Permissions p ON p.Id = rp.PermissionId
            WHERE p.PermissionCode = N'Platform.Vehicles.ViewAll';

            DELETE FROM Permissions WHERE PermissionCode = N'Platform.Vehicles.ViewAll';
            """, cancellationToken: cancellationToken));

        // Ensure SUPER_ADMIN exists in platform tenant
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM Roles
                WHERE TenantId = @TenantId AND Code = N'SUPER_ADMIN')
            INSERT INTO Roles (
                TenantId, Name, Code, IsSystem, IsActive, CreatedAt,
                DisplayName, Description, Category, SortOrder, Visible, RoleType, ScopeLevel)
            VALUES (
                @TenantId, N'Super Admin', N'SUPER_ADMIN', 1, 1, SYSUTCDATETIME(),
                N'Super Admin', N'Platform-wide administration', N'Platform', 5, 1, N'System', N'Company');
            """,
            new { TenantId = PlatformTenant.Id },
            cancellationToken: cancellationToken));

        // Grant SUPER_ADMIN the full catalog in platform tenant
        var allCodes = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT PermissionCode FROM Permissions ORDER BY PermissionCode",
            cancellationToken: cancellationToken))).ToList();

        await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
            connection,
            PlatformTenant.Id,
            PlatformRoles.SuperAdmin,
            allCodes,
            cancellationToken);

        // Create PLATFORM_AUDITOR in platform tenant
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM Roles
                WHERE TenantId = @TenantId AND Code = N'PLATFORM_AUDITOR')
            INSERT INTO Roles (
                TenantId, Name, Code, IsSystem, IsActive, CreatedAt,
                DisplayName, Description, Category, SortOrder, Visible, RoleType, ScopeLevel)
            VALUES (
                @TenantId, N'Platform Auditor', N'PLATFORM_AUDITOR', 1, 1, SYSUTCDATETIME(),
                N'Platform Auditor', N'Read-only cross-tenant platform auditor', N'Platform', 7, 1, N'System', N'Company');
            ELSE
            UPDATE Roles SET
                IsActive = 1,
                DisplayName = N'Platform Auditor',
                Description = N'Read-only cross-tenant platform auditor',
                Category = N'Platform',
                SortOrder = 7,
                Visible = 1,
                RoleType = N'System',
                ScopeLevel = N'Company',
                IsSystem = 1
            WHERE TenantId = @TenantId AND Code = N'PLATFORM_AUDITOR';
            """,
            new { TenantId = PlatformTenant.Id },
            cancellationToken: cancellationToken));

        var auditorPerms = BuildAuditorPermissionList(allCodes);
        await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
            connection,
            PlatformTenant.Id,
            PlatformRoles.PlatformAuditor,
            auditorPerms,
            cancellationToken);

        // Delete platform-scope RolePermissions from roles outside platform tenant
        var removedOutside = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE rp
            FROM RolePermissions rp
            INNER JOIN Roles r ON r.Id = rp.RoleId
            INNER JOIN Permissions p ON p.Id = rp.PermissionId
            WHERE r.TenantId <> @PlatformTenantId
              AND (
                    p.PermissionCode LIKE N'Gps.Command%'
                 OR p.PermissionCode LIKE N'Platform.Tenants.%'
                 OR p.PermissionCode LIKE N'Platform.Vehicles.%'
                 OR p.PermissionCode LIKE N'Platform.Gps.%'
                 OR p.PermissionCode LIKE N'Platform.Migrations.%'
                 OR p.PermissionCode LIKE N'Platform.System.%'
              );
            """,
            new { PlatformTenantId = PlatformTenant.Id },
            cancellationToken: cancellationToken));

        // Deactivate SUPER_ADMIN / PLATFORM_AUDITOR outside platform tenant when unused
        var strayRoles = (await connection.QueryAsync<(int RoleId, int TenantId, string Code, int Users)>(
            new CommandDefinition("""
                SELECT r.Id AS RoleId, r.TenantId, r.Code,
                       (SELECT COUNT(*) FROM UserRoles ur WHERE ur.RoleId = r.Id) AS Users
                FROM Roles r
                WHERE r.TenantId <> @PlatformTenantId
                  AND UPPER(r.Code) IN (N'SUPER_ADMIN', N'PLATFORM_AUDITOR')
                  AND r.IsActive = 1
                """,
                new { PlatformTenantId = PlatformTenant.Id },
                cancellationToken: cancellationToken))).ToList();

        foreach (var stray in strayRoles)
        {
            if (stray.Users == 0)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE Roles SET IsActive = 0, UpdatedAt = SYSUTCDATETIME()
                    WHERE Id = @RoleId;
                    """, new { stray.RoleId }, cancellationToken: cancellationToken));
                logger.LogInformation(
                    "Deactivated unused {RoleCode} role Id={RoleId} in TenantId={TenantId}.",
                    stray.Code, stray.RoleId, stray.TenantId);
            }
            else
            {
                var userIds = (await connection.QueryAsync<int>(new CommandDefinition("""
                    SELECT ur.UserId FROM UserRoles ur WHERE ur.RoleId = @RoleId
                    """, new { stray.RoleId }, cancellationToken: cancellationToken))).ToList();
                logger.LogWarning(
                    "Left active {RoleCode} role Id={RoleId} in TenantId={TenantId} with {UserCount} user(s): {UserIds}. Resolve manually.",
                    stray.Code, stray.RoleId, stray.TenantId, stray.Users, string.Join(", ", userIds));
            }
        }

        // Insert-only template sync so TENANT_ADMIN explicitly holds Platform.* (shortcut removal)
        var tenantIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Tenants ORDER BY Id",
            cancellationToken: cancellationToken))).ToList();

        foreach (var tenantId in tenantIds)
        {
            await TenantRolePermissionSeeder.SeedSystemRolePermissionsForTenantAsync(
                connection, tenantId, cancellationToken);
        }

        logger.LogInformation(
            "PlatformOperatorPermissionsMigration applied. SUPER_ADMIN catalog={CatalogCount}, PLATFORM_AUDITOR grants={AuditorCount}, strippedOutside={Removed}, tenantsSynced={TenantCount}.",
            allCodes.Count,
            auditorPerms.Count,
            removedOutside,
            tenantIds.Count);
    }

    internal static List<string> BuildAuditorPermissionList(IEnumerable<string> catalog)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PlatformPermissions.TenantsView,
            PlatformPermissions.TenantsDataRead,
            PlatformPermissions.TenantsAuditReadAll,
            PlatformPermissions.TenantsUsersReadAll,
        };

        foreach (var code in catalog)
        {
            if (string.IsNullOrWhiteSpace(code)) continue;
            if (code.StartsWith("Gps.Command", StringComparison.OrdinalIgnoreCase)) continue;
            if (code.StartsWith("Platform.System.", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(code, PlatformPermissions.TenantsManage, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(code, PlatformPermissions.TenantsImpersonate, StringComparison.OrdinalIgnoreCase)) continue;

            if (code.EndsWith(".View", StringComparison.OrdinalIgnoreCase)
                || code.EndsWith(".ViewAll", StringComparison.OrdinalIgnoreCase)
                || code.EndsWith(".ViewPerformance", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, "Report.View", StringComparison.OrdinalIgnoreCase))
            {
                set.Add(code);
            }
        }

        return set.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
