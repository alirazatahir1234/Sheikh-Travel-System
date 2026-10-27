using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

public static class DriverWalletPermissionsMigration
{
    private static readonly (string Module, string Code, string Desc)[] Permissions =
    [
        ("Finance", FinancePermissions.DriverWalletView, "View driver wallets, ledger, and cash-out requests"),
        ("Finance", FinancePermissions.DriverWalletManage, "Approve, reject, and process driver cash-outs in bulk"),
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

        var allCodes = new[] { FinancePermissions.DriverWalletView, FinancePermissions.DriverWalletManage };
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "SUPER_ADMIN", allCodes, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN", allCodes, cancellationToken);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "ACCOUNTANT", allCodes, cancellationToken);

        logger.LogInformation("DriverWalletPermissionsMigration applied.");
    }
}
