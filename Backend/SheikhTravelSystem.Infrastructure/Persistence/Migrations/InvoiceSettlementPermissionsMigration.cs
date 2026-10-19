using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds Invoice.Manage + Settlement.View/Manage and assigns to finance roles.
/// </summary>
public static class InvoiceSettlementPermissionsMigration
{
    private static readonly (string Module, string Code, string Desc)[] Permissions =
    [
        ("Finance", FinancePermissions.InvoiceManage, "Create, void, and mark invoices paid"),
        ("Finance", FinancePermissions.SettlementView, "View driver settlements"),
        ("Finance", FinancePermissions.SettlementManage, "Generate, approve, adjust, and mark settlements paid"),
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        foreach (var (module, code, desc) in Permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (@Module, @Code, @Desc);
                """, new { Module = module, Code = code, Desc = desc },
                cancellationToken: cancellationToken));
        }

        // Ensure Invoice.View exists (seeded in PlatformSchemaMigration for fresh DBs).
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
            AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
            INSERT INTO Permissions (ModuleName, PermissionCode, Description)
            VALUES (N'Finance', @Code, N'View invoices');
            """, new { Code = FinancePermissions.InvoiceView },
            cancellationToken: cancellationToken));

        var allCodes = new[]
        {
            FinancePermissions.InvoiceView,
            FinancePermissions.InvoiceManage,
            FinancePermissions.SettlementView,
            FinancePermissions.SettlementManage
        };

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "SUPER_ADMIN", allCodes, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN", allCodes, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "ACCOUNTANT", allCodes, cancellationToken);

        // Drivers can view their own settlements via driver-app (role claim); ERP uses Settlement.View.
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "DRIVER",
            [FinancePermissions.SettlementView],
            cancellationToken);

        logger.LogInformation(
            "InvoiceSettlementPermissionsMigration applied ({Count} permissions).",
            Permissions.Length);
    }
}
