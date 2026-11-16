using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// SG-07: TargetTenantId on AuditEvents, append-only triggers, SG-36 menu/route cleanup,
/// and seed catalog keys for cross-tenant / billing / PII / impersonation.
/// </summary>
public static class Sg07AuditTrailTargetTenantAndAppendOnlyMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // Separate batches: SQL Server compiles each batch before execution, so ADD COLUMN
        // and any statement that references TargetTenantId cannot share one batch.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('AuditEvents', 'TargetTenantId') IS NULL
                ALTER TABLE AuditEvents ADD TargetTenantId INT NULL;
            """, cancellationToken: cancellationToken));

        // NULL = platform aggregate / no single target (never "forgot to set").
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('AuditEvents', 'TargetTenantId') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AuditEvents_TargetTenant')
                ALTER TABLE AuditEvents WITH CHECK
                ADD CONSTRAINT FK_AuditEvents_TargetTenant
                FOREIGN KEY (TargetTenantId) REFERENCES Tenants(Id);
            """, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('AuditEvents', 'TargetTenantId') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_AuditEvents_TargetTenant_CreatedOn'
                      AND object_id = OBJECT_ID('AuditEvents'))
                CREATE INDEX IX_AuditEvents_TargetTenant_CreatedOn
                ON AuditEvents (TargetTenantId, CreatedOn DESC)
                WHERE TargetTenantId IS NOT NULL;
            """, cancellationToken: cancellationToken));

        // Append-only: block UPDATE/DELETE on AuditEvents (SG-07).
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'dbo.TR_AuditEvents_NoUpdate', N'TR') IS NULL
            EXEC(N'
            CREATE TRIGGER dbo.TR_AuditEvents_NoUpdate ON dbo.AuditEvents
            AFTER UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 50001, ''AuditEvents is append-only: UPDATE is not allowed.'', 1;
            END');

            IF OBJECT_ID(N'dbo.TR_AuditEvents_NoDelete', N'TR') IS NULL
            EXEC(N'
            CREATE TRIGGER dbo.TR_AuditEvents_NoDelete ON dbo.AuditEvents
            AFTER DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 50002, ''AuditEvents is append-only: DELETE is not allowed.'', 1;
            END');
            """, cancellationToken: cancellationToken));

        // SG-36: keep /platform/audit-center; hide/redirect legacy /audit-logs menu rows.
        // Do NOT rename multiple rows to Name='Audit Center' in one shot — UX_PlatformMenus_ModuleId_Name.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus')
            BEGIN
                -- Retarget legacy routes only (leave Name alone to avoid unique-index collisions).
                UPDATE PlatformMenus
                SET Route = N'/platform/audit-center',
                    DisplayName = COALESCE(NULLIF(DisplayName, N''), N'Audit Center'),
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Route IN (N'/audit-logs', N'/AuditLogs', N'/audit-log');

                -- Prefer FeatureKey=audit-logs as the canonical Audit Center name when free.
                UPDATE pm
                SET Name = N'Audit Center',
                    DisplayName = COALESCE(NULLIF(pm.DisplayName, N''), N'Audit Center'),
                    Route = N'/platform/audit-center',
                    UpdatedAt = SYSUTCDATETIME()
                FROM PlatformMenus pm
                WHERE pm.FeatureKey = N'audit-logs'
                  AND pm.Name <> N'Audit Center'
                  AND NOT EXISTS (
                      SELECT 1 FROM PlatformMenus other
                      WHERE other.ModuleId = pm.ModuleId
                        AND other.Name = N'Audit Center'
                        AND other.Id <> pm.Id);

                -- Prefer a single visible Audit Center under platform module.
                ;WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY Route
                               ORDER BY CASE WHEN FeatureKey = N'audit-logs' THEN 0 ELSE 1 END,
                                        CASE WHEN ModuleKey = N'platform' OR ModuleId IN (
                                            SELECT Id FROM PlatformModules WHERE ModuleKey = N'platform') THEN 0 ELSE 1 END,
                                        Id) AS rn
                    FROM PlatformMenus
                    WHERE Route = N'/platform/audit-center' AND IsActive = 1
                )
                UPDATE pm SET IsActive = 0, Visible = 0, UpdatedAt = SYSUTCDATETIME()
                FROM PlatformMenus pm
                INNER JOIN ranked r ON r.Id = pm.Id
                WHERE r.rn > 1;
            END
            """, cancellationToken: cancellationToken));

        foreach (var (eventKey, display, category, severity, description, sort) in new[]
                 {
                     ("platform.cross_tenant.read", "Cross-tenant data read", "Platform", "Warning",
                         "Platform operator read another company's data (or an all-companies aggregate).", 360),
                     ("billing.subscription.change", "Subscription changed", "Billing", "Critical",
                         "Tenant subscription / billing lifecycle change.", 370),
                     ("privacy.pii.reveal", "PII revealed", "Privacy", "Critical",
                         "Restricted personal data was unmasked (SG-18).", 380),
                     ("platform.impersonation", "Tenant impersonation", "Security", "Critical",
                         "Actor used Platform.Tenants.Impersonate. Endpoint not shipped yet — key reserved.", 390),
                     ("roles.assignment", "User roles assigned", "Authorization", "Warning",
                         "User role membership changed.", 260)
                 })
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM AuditEventDefinitions WHERE EventKey = @EventKey)
                    UPDATE AuditEventDefinitions SET
                        DisplayName = @DisplayName,
                        Category = @Category,
                        Severity = @Severity,
                        Description = COALESCE(Description, @Description),
                        SortOrder = @SortOrder,
                        Visible = 1,
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
                        @SortOrder, 1, 1, 1);
                """,
                new
                {
                    EventKey = eventKey,
                    DisplayName = display,
                    Category = category,
                    Severity = severity,
                    Description = description,
                    SortOrder = sort
                },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation(
            "SG-07 migration applied: TargetTenantId, append-only triggers, Audit Center menu cleanup.");
    }
}
