using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 14 Audit Center: event definition registry + AuditEvents store + permissions/menu.
/// </summary>
public static class AuditCenterFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuditEventDefinitions')
            CREATE TABLE AuditEventDefinitions (
                EventKey NVARCHAR(100) NOT NULL CONSTRAINT PK_AuditEventDefinitions PRIMARY KEY,
                DisplayName NVARCHAR(200) NOT NULL,
                Category NVARCHAR(100) NOT NULL,
                Severity NVARCHAR(40) NOT NULL CONSTRAINT DF_AuditEventDefinitions_Severity DEFAULT (N'Information'),
                Description NVARCHAR(500) NULL,
                SortOrder INT NOT NULL CONSTRAINT DF_AuditEventDefinitions_SortOrder DEFAULT (0),
                Visible BIT NOT NULL CONSTRAINT DF_AuditEventDefinitions_Visible DEFAULT (1),
                IsActive BIT NOT NULL CONSTRAINT DF_AuditEventDefinitions_IsActive DEFAULT (1),
                IsSystem BIT NOT NULL CONSTRAINT DF_AuditEventDefinitions_IsSystem DEFAULT (1),
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_AuditEventDefinitions_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt DATETIME2 NULL
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AuditEvents')
            CREATE TABLE AuditEvents (
                Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditEvents PRIMARY KEY,
                TenantId INT NOT NULL,
                UserId INT NULL,
                EventKey NVARCHAR(100) NOT NULL,
                EntityType NVARCHAR(100) NULL,
                EntityId INT NULL,
                Action NVARCHAR(100) NULL,
                OldValues NVARCHAR(MAX) NULL,
                NewValues NVARCHAR(MAX) NULL,
                IpAddress NVARCHAR(64) NULL,
                UserAgent NVARCHAR(256) NULL,
                CorrelationId NVARCHAR(64) NULL,
                Success BIT NOT NULL CONSTRAINT DF_AuditEvents_Success DEFAULT (1),
                Message NVARCHAR(500) NULL,
                CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_AuditEvents_CreatedOn DEFAULT (SYSUTCDATETIME()),
                CONSTRAINT FK_AuditEvents_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT FK_AuditEvents_Definitions FOREIGN KEY (EventKey) REFERENCES AuditEventDefinitions(EventKey)
            );

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_AuditEvents_Tenant_CreatedOn' AND object_id = OBJECT_ID('AuditEvents'))
                CREATE INDEX IX_AuditEvents_Tenant_CreatedOn ON AuditEvents (TenantId, CreatedOn DESC);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_AuditEvents_Tenant_EventKey' AND object_id = OBJECT_ID('AuditEvents'))
                CREATE INDEX IX_AuditEvents_Tenant_EventKey ON AuditEvents (TenantId, EventKey);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_AuditEvents_Tenant_Entity' AND object_id = OBJECT_ID('AuditEvents'))
                CREATE INDEX IX_AuditEvents_Tenant_Entity ON AuditEvents (TenantId, EntityType, EntityId);
            """, cancellationToken: cancellationToken));

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken)) == 1)
        {
            foreach (var (code, desc) in new[]
                     {
                         (PlatformPermissions.AuditView, "View Audit Center events"),
                         (PlatformPermissions.AuditManage, "Manage Audit Center (retention metadata)")
                     })
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                    INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                    VALUES (N'Platform', @Code, @Desc);
                    """,
                    new { Code = code, Desc = desc },
                    cancellationToken: cancellationToken));
            }

            var codes = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT PermissionCode FROM Permissions", cancellationToken: cancellationToken))).ToList();
            await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
                connection, tenantId: 1, "SUPER_ADMIN", codes, cancellationToken);
            await TenantRolePermissionSeeder.AssignRolePermissionsForTenantAsync(
                connection, tenantId: 1, "TENANT_ADMIN",
                TenantRolePermissionTemplates.TenantAdmin, cancellationToken);
        }

        foreach (var seed in AuditEventRegistrySeed.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM AuditEventDefinitions WHERE EventKey = @EventKey)
                    UPDATE AuditEventDefinitions SET
                        DisplayName = @DisplayName,
                        Category = @Category,
                        Severity = @Severity,
                        Description = COALESCE(Description, @Description),
                        SortOrder = @SortOrder,
                        Visible = @Visible,
                        IsActive = 1,
                        IsSystem = 1,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE EventKey = @EventKey;
                ELSE
                    INSERT INTO AuditEventDefinitions (
                        EventKey, DisplayName, Category, Severity, Description,
                        SortOrder, Visible, IsActive, IsSystem)
                    VALUES (
                        @EventKey, @DisplayName, @Category, @Severity, @Description,
                        @SortOrder, @Visible, 1, 1);
                """,
                new
                {
                    seed.EventKey,
                    seed.DisplayName,
                    seed.Category,
                    seed.Severity,
                    seed.Description,
                    seed.SortOrder,
                    Visible = seed.Visible
                },
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'platform')
            AND NOT EXISTS (
                SELECT 1 FROM PlatformMenus pm
                WHERE pm.Route = N'/platform/audit-center' AND (pm.IsDeleted = 0 OR pm.IsDeleted IS NULL))
            BEGIN
                INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                    DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                SELECT m.Id, NULL, N'Audit', N'/platform/audit-center', N'history',
                       N'Platform.Audit.View', 49, 1,
                       N'Audit Center', N'Company audit event explorer', N'Platform', 1, N'audit-logs', NULL, 0, SYSUTCDATETIME()
                FROM PlatformModules m WHERE m.ModuleKey = N'platform';
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("AuditCenterFoundationMigration applied ({Count} event definitions).",
            AuditEventRegistrySeed.All.Count);
    }
}
