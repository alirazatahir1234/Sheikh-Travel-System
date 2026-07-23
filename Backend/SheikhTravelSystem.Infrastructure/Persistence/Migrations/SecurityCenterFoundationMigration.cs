using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Stage 13 Security Center: policy registry + tenant values + Users lockout/password columns.
/// </summary>
public static class SecurityCenterFoundationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SecurityPolicyDefinitions')
            CREATE TABLE SecurityPolicyDefinitions (
                PolicyKey NVARCHAR(100) NOT NULL CONSTRAINT PK_SecurityPolicyDefinitions PRIMARY KEY,
                DisplayName NVARCHAR(200) NOT NULL,
                Category NVARCHAR(100) NOT NULL,
                Description NVARCHAR(500) NULL,
                DefaultValue NVARCHAR(MAX) NOT NULL CONSTRAINT DF_SecurityPolicyDefinitions_DefaultValue DEFAULT (N''),
                ValueType NVARCHAR(40) NOT NULL CONSTRAINT DF_SecurityPolicyDefinitions_ValueType DEFAULT (N'String'),
                SortOrder INT NOT NULL CONSTRAINT DF_SecurityPolicyDefinitions_SortOrder DEFAULT (0),
                Visible BIT NOT NULL CONSTRAINT DF_SecurityPolicyDefinitions_Visible DEFAULT (1),
                IsActive BIT NOT NULL CONSTRAINT DF_SecurityPolicyDefinitions_IsActive DEFAULT (1),
                IsSystem BIT NOT NULL CONSTRAINT DF_SecurityPolicyDefinitions_IsSystem DEFAULT (1),
                CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SecurityPolicyDefinitions_CreatedAt DEFAULT (SYSUTCDATETIME()),
                UpdatedAt DATETIME2 NULL
            );

            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantSecurityPolicies')
            CREATE TABLE TenantSecurityPolicies (
                TenantId INT NOT NULL,
                PolicyKey NVARCHAR(100) NOT NULL,
                PolicyValue NVARCHAR(MAX) NOT NULL CONSTRAINT DF_TenantSecurityPolicies_PolicyValue DEFAULT (N''),
                UpdatedBy INT NULL,
                UpdatedDate DATETIME2 NULL,
                CONSTRAINT PK_TenantSecurityPolicies PRIMARY KEY (TenantId, PolicyKey),
                CONSTRAINT FK_TenantSecurityPolicies_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT FK_TenantSecurityPolicies_Definitions FOREIGN KEY (PolicyKey) REFERENCES SecurityPolicyDefinitions(PolicyKey)
            );

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_TenantSecurityPolicies_PolicyKey' AND object_id = OBJECT_ID('TenantSecurityPolicies'))
                CREATE INDEX IX_TenantSecurityPolicies_PolicyKey ON TenantSecurityPolicies (PolicyKey);
            """, cancellationToken: cancellationToken));

        await AddColumnIfMissingAsync(connection, "Users", "FailedLoginAttempts", "INT NOT NULL CONSTRAINT DF_Users_FailedLoginAttempts DEFAULT (0)", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "LockoutEndUtc", "DATETIME2 NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "Users", "PasswordChangedAt", "DATETIME2 NULL", cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users SET PasswordChangedAt = COALESCE(PasswordChangedAt, CreatedAt, SYSUTCDATETIME())
            WHERE PasswordChangedAt IS NULL AND IsDeleted = 0;
            """, cancellationToken: cancellationToken));

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken)) == 1)
        {
            foreach (var (code, desc) in new[]
                     {
                         (PlatformPermissions.SecurityView, "View Security Center policies"),
                         (PlatformPermissions.SecurityManage, "Manage company security policies")
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

        foreach (var seed in SecurityPolicyRegistrySeed.All)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM SecurityPolicyDefinitions WHERE PolicyKey = @PolicyKey)
                    UPDATE SecurityPolicyDefinitions SET
                        DisplayName = @DisplayName,
                        Category = @Category,
                        Description = COALESCE(Description, @Description),
                        DefaultValue = COALESCE(NULLIF(LTRIM(RTRIM(DefaultValue)), ''), @DefaultValue),
                        ValueType = @ValueType,
                        SortOrder = @SortOrder,
                        Visible = @Visible,
                        IsActive = 1,
                        IsSystem = 1,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE PolicyKey = @PolicyKey;
                ELSE
                    INSERT INTO SecurityPolicyDefinitions (
                        PolicyKey, DisplayName, Category, Description, DefaultValue, ValueType,
                        SortOrder, Visible, IsActive, IsSystem)
                    VALUES (
                        @PolicyKey, @DisplayName, @Category, @Description, @DefaultValue, @ValueType,
                        @SortOrder, @Visible, 1, 1);
                """,
                new
                {
                    seed.PolicyKey,
                    seed.DisplayName,
                    seed.Category,
                    seed.Description,
                    seed.DefaultValue,
                    seed.ValueType,
                    seed.SortOrder,
                    Visible = seed.Visible
                },
                cancellationToken: cancellationToken));
        }

        // Backfill tenant policies from legacy TenantSecuritySettings where present.
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO TenantSecurityPolicies (TenantId, PolicyKey, PolicyValue, UpdatedDate)
            SELECT s.TenantId, d.PolicyKey,
                CASE d.PolicyKey
                    WHEN N'password.max_age_days' THEN CAST(COALESCE(s.PasswordExpiryDays, 90) AS NVARCHAR(32))
                    WHEN N'session.idle_timeout_minutes' THEN CAST(COALESCE(s.SessionTimeoutMinutes, 30) AS NVARCHAR(32))
                    WHEN N'compliance.gdpr_logging' THEN CASE WHEN s.IsGdprEnabled = 1 THEN N'true' ELSE N'false' END
                    WHEN N'compliance.mfa_required' THEN CASE WHEN s.IsMfaRequired = 1 THEN N'true' ELSE N'false' END
                    WHEN N'compliance.vat_enabled' THEN CASE WHEN s.IsVatEnabled = 1 THEN N'true' ELSE N'false' END
                    WHEN N'audit.level' THEN CASE WHEN s.IsAuditLoggingEnabled = 1 THEN N'Always' ELSE N'Disabled' END
                    ELSE d.DefaultValue
                END,
                SYSUTCDATETIME()
            FROM TenantSecuritySettings s
            CROSS JOIN SecurityPolicyDefinitions d
            WHERE d.PolicyKey IN (
                N'password.max_age_days', N'session.idle_timeout_minutes',
                N'compliance.gdpr_logging', N'compliance.mfa_required', N'compliance.vat_enabled', N'audit.level')
              AND NOT EXISTS (
                SELECT 1 FROM TenantSecurityPolicies p
                WHERE p.TenantId = s.TenantId AND p.PolicyKey = d.PolicyKey);
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'platform')
            AND NOT EXISTS (
                SELECT 1 FROM PlatformMenus pm
                WHERE pm.Route = N'/platform/security-center' AND (pm.IsDeleted = 0 OR pm.IsDeleted IS NULL))
            BEGIN
                INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                    DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                SELECT m.Id, NULL, N'Security', N'/platform/security-center', N'security',
                       N'Platform.Security.View', 48, 1,
                       N'Security Center', N'Company security policy registry', N'Platform', 1, NULL, NULL, 0, SYSUTCDATETIME()
                FROM PlatformModules m WHERE m.ModuleKey = N'platform';
            END
            """, cancellationToken: cancellationToken));

        logger.LogInformation("SecurityCenterFoundationMigration applied ({Count} policy definitions).",
            SecurityPolicyRegistrySeed.All.Count);
    }

    private static async Task AddColumnIfMissingAsync(
        IDbConnection connection,
        string table,
        string column,
        string sqlType,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition($"""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @Table)
            AND NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column)
            EXEC(N'ALTER TABLE [{table}] ADD [{column}] {sqlType}');
            """,
            new { Table = table, Column = column },
            cancellationToken: cancellationToken));
    }
}
