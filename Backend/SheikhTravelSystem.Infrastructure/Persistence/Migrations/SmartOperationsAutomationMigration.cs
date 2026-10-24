using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Smart Operations Automation: SmartAlerts store + optional tank capacity / invoice due date columns.
/// Registered in DatabaseMigrationRegistry — applied on local/dev API startup only, never force-applied to production out-of-band.
/// </summary>
public static class SmartOperationsAutomationMigration
{
    private static readonly (string Module, string Code, string Desc)[] Permissions =
    [
        ("Operations", SmartOpsPermissions.AlertView, "View Smart Operations alerts"),
        ("Operations", SmartOpsPermissions.AlertManage, "Acknowledge, resolve, or dismiss Smart Operations alerts"),
    ];

    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF OBJECT_ID(N'dbo.SmartAlerts', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.SmartAlerts (
                        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmartAlerts PRIMARY KEY,
                        TenantId INT NOT NULL,
                        RuleType NVARCHAR(40) NOT NULL,
                        Severity NVARCHAR(20) NOT NULL CONSTRAINT DF_SmartAlerts_Severity DEFAULT N'Warning',
                        Message NVARCHAR(1000) NOT NULL,
                        RelatedEntityType NVARCHAR(60) NULL,
                        RelatedEntityId INT NULL,
                        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_SmartAlerts_Status DEFAULT N'New',
                        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SmartAlerts_CreatedAt DEFAULT SYSUTCDATETIME(),
                        AcknowledgedAt DATETIME2 NULL,
                        AcknowledgedBy NVARCHAR(100) NULL,
                        ResolvedAt DATETIME2 NULL,
                        ResolvedBy NVARCHAR(100) NULL,
                        DeduplicationKey NVARCHAR(200) NOT NULL,
                        CONSTRAINT UQ_SmartAlerts_Tenant_Dedupe UNIQUE (TenantId, DeduplicationKey),
                        CONSTRAINT FK_SmartAlerts_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
                    );
                    CREATE INDEX IX_SmartAlerts_Tenant_Status_Created
                        ON dbo.SmartAlerts (TenantId, Status, CreatedAt DESC);
                    CREATE INDEX IX_SmartAlerts_Tenant_Rule
                        ON dbo.SmartAlerts (TenantId, RuleType, Status);
                END

                IF COL_LENGTH('dbo.Vehicles', 'TankCapacityLiters') IS NULL
                    ALTER TABLE dbo.Vehicles ADD TankCapacityLiters DECIMAL(10,2) NULL;

                IF OBJECT_ID(N'dbo.CustomerInvoices', N'U') IS NOT NULL
                   AND COL_LENGTH('dbo.CustomerInvoices', 'DueDate') IS NULL
                BEGIN
                    ALTER TABLE dbo.CustomerInvoices ADD DueDate DATETIME2 NULL;
                    EXEC(N'UPDATE dbo.CustomerInvoices SET DueDate = IssuedAt WHERE DueDate IS NULL AND IssuedAt IS NOT NULL');
                END
                """, cancellationToken: cancellationToken));

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

            var codes = Permissions.Select(p => p.Code).ToArray();
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, "SUPER_ADMIN", codes, cancellationToken);
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, "TENANT_ADMIN", codes, cancellationToken);
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, "OWNER", codes, cancellationToken);
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, "FLEET_MANAGER", codes, cancellationToken);
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, "DISPATCHER", codes, cancellationToken);

            logger.LogInformation("SmartOperationsAutomationMigration applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SmartOperationsAutomationMigration failed.");
            throw;
        }
    }
}
