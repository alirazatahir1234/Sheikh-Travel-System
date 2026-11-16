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

        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH('AuditEvents', 'TargetTenantId') IS NULL
            BEGIN
                ALTER TABLE AuditEvents ADD TargetTenantId INT NULL;
                -- NULL = platform aggregate / no single target (never "forgot to set").
                -- FK optional: aggregates park under platform TenantId with TargetTenantId NULL.
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AuditEvents_TargetTenant')
                    ALTER TABLE AuditEvents WITH CHECK
                    ADD CONSTRAINT FK_AuditEvents_TargetTenant
                    FOREIGN KEY (TargetTenantId) REFERENCES Tenants(Id);
            END

            IF NOT EXISTS (
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
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus')
            BEGIN
                UPDATE PlatformMenus
                SET Route = N'/platform/audit-center',
                    DisplayName = COALESCE(DisplayName, N'Audit Center'),
                    Name = CASE WHEN Name IN (N'Audit Logs', N'Audit') THEN N'Audit Center' ELSE Name END,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Route IN (N'/audit-logs', N'/AuditLogs', N'/audit-log')
                   OR FeatureKey = N'audit-logs';

                -- Prefer a single visible Audit Center under platform module.
                ;WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY Route
                               ORDER BY CASE WHEN ModuleKey = N'platform' OR ModuleId IN (
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
