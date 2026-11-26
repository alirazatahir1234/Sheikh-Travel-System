using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// TravelNexus Phase 7: reminder rules (per tenant, rule type and lead time), the reminder dispatch log (one row per rule,
/// entity, due date, lead and audience — the dedupe key), quiet hours for customer messages, the <c>travel_reminder</c>
/// notification templates, Phase 7 permissions and the Dashboard / Reminders / Reports menu rows. Additive and idempotent.
/// </summary>
public static class TravelNexusRemindersMigration
{
    private static readonly (string Code, string Desc)[] PermissionRows =
    [
        (TravelPermissions.ReminderView, "View reminder rules and the reminder log"),
        (TravelPermissions.ReminderManage, "Configure reminder rules and run reminders"),
        (TravelPermissions.DashboardView, "View the TravelNexus dashboard"),
        (TravelPermissions.ReportView, "View operational travel reports"),
    ];

    public static async Task ApplyAsync(IDbConnectionFactory dbFactory, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        // ── Settings, rules, dispatch log ────────────────────────────────────────
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'dbo.TravelSettings', N'ReminderQuietStartHour') IS NULL
                ALTER TABLE dbo.TravelSettings ADD ReminderQuietStartHour TINYINT NOT NULL CONSTRAINT DF_TravelSettings_ReminderQuietStart DEFAULT (21);
            IF COL_LENGTH(N'dbo.TravelSettings', N'ReminderQuietEndHour') IS NULL
                ALTER TABLE dbo.TravelSettings ADD ReminderQuietEndHour TINYINT NOT NULL CONSTRAINT DF_TravelSettings_ReminderQuietEnd DEFAULT (8);

            IF OBJECT_ID(N'dbo.TravelReminderRules', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelReminderRules (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelReminderRules PRIMARY KEY,
                    TenantId INT NOT NULL,
                    RuleType NVARCHAR(24) NOT NULL,
                    LeadDays INT NOT NULL,
                    IsEnabled BIT NOT NULL CONSTRAINT DF_TravelReminderRules_Enabled DEFAULT (1),
                    StaffInApp BIT NOT NULL CONSTRAINT DF_TravelReminderRules_StaffInApp DEFAULT (1),
                    StaffEmail BIT NOT NULL CONSTRAINT DF_TravelReminderRules_StaffEmail DEFAULT (0),
                    CustomerEmail BIT NOT NULL CONSTRAINT DF_TravelReminderRules_CustomerEmail DEFAULT (0),
                    CustomerSms BIT NOT NULL CONSTRAINT DF_TravelReminderRules_CustomerSms DEFAULT (0),
                    CustomerWhatsApp BIT NOT NULL CONSTRAINT DF_TravelReminderRules_CustomerWhatsApp DEFAULT (0),
                    CustomerTemplate NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelReminderRules_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CreatedBy INT NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy INT NULL,
                    CONSTRAINT FK_TravelReminderRules_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT CK_TravelReminderRules_Type CHECK (RuleType IN (N'PassportExpiry', N'VisaAppointment', N'MissingDocuments',
                        N'VisaDecisionOverdue', N'VisaStatusChanged', N'TicketingDeadline', N'FlightDeparture', N'HotelCheckIn',
                        N'InvoiceDue', N'InvoiceOverdue', N'EnquiryFollowUp', N'GroupDeparture')),
                    CONSTRAINT CK_TravelReminderRules_Lead CHECK (LeadDays >= 0 AND LeadDays <= 365)
                );
                CREATE UNIQUE INDEX UX_TravelReminderRules_Tenant_Type_Lead ON dbo.TravelReminderRules (TenantId, RuleType, LeadDays);
            END

            IF OBJECT_ID(N'dbo.TravelReminders', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TravelReminders (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TravelReminders PRIMARY KEY,
                    TenantId INT NOT NULL,
                    RuleType NVARCHAR(24) NOT NULL,
                    LeadDays INT NOT NULL,
                    EntityType NVARCHAR(16) NOT NULL,
                    EntityId INT NOT NULL,
                    ContextLabel NVARCHAR(200) NOT NULL,
                    DueAt DATETIME2 NOT NULL,
                    Audience NVARCHAR(8) NOT NULL,
                    DedupeKey NVARCHAR(160) NOT NULL,
                    RecipientUserId INT NULL,
                    RecipientCustomerId INT NULL,
                    Channels NVARCHAR(100) NOT NULL CONSTRAINT DF_TravelReminders_Channels DEFAULT (N''),
                    Status NVARCHAR(8) NOT NULL,
                    FailureReason NVARCHAR(500) NULL,
                    NotificationId INT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Body NVARCHAR(2000) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_TravelReminders_CreatedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_TravelReminders_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                    CONSTRAINT CK_TravelReminders_Entity CHECK (EntityType IN (N'Traveler', N'VisaCase', N'FlightBooking', N'HotelBooking', N'Invoice', N'Enquiry', N'Group')),
                    CONSTRAINT CK_TravelReminders_Audience CHECK (Audience IN (N'Staff', N'Customer')),
                    CONSTRAINT CK_TravelReminders_Status CHECK (Status IN (N'Pending', N'Sent', N'Skipped', N'Failed'))
                );
                -- The dedupe key is the whole point: one reminder per rule, entity, due date, lead time and audience.
                CREATE UNIQUE INDEX UX_TravelReminders_Tenant_Dedupe ON dbo.TravelReminders (TenantId, DedupeKey);
                CREATE INDEX IX_TravelReminders_Tenant_Created ON dbo.TravelReminders (TenantId, CreatedAt DESC);
                CREATE INDEX IX_TravelReminders_Tenant_Entity ON dbo.TravelReminders (TenantId, EntityType, EntityId);
            END
            """, cancellationToken: cancellationToken));

        // ── Notification templates (platform rows; a later tenant-specific row would need a distinct key) ──
        foreach (var channel in new[] { "InApp", "Email", "Sms" })
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF OBJECT_ID(N'dbo.NotificationTemplates', N'U') IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM NotificationTemplates WHERE TemplateKey = N'travel_reminder' AND Channel = @Channel AND IsDeleted = 0)
                BEGIN
                    IF COL_LENGTH(N'dbo.NotificationTemplates', N'Variables') IS NOT NULL
                        INSERT INTO NotificationTemplates (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Variables, CreatedAt, IsDeleted)
                        VALUES (N'travel_reminder', N'TravelNexus reminder', N'{{Title}}', N'{{Message}}', @Channel, 1, N'["Title","Message"]', GETUTCDATE(), 0);
                    ELSE
                        INSERT INTO NotificationTemplates (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, CreatedAt, IsDeleted)
                        VALUES (N'travel_reminder', N'TravelNexus reminder', N'{{Title}}', N'{{Message}}', @Channel, 1, GETUTCDATE(), 0);
                END
                """, new { Channel = channel }, cancellationToken: cancellationToken));
        }

        foreach (var (code, desc) in PermissionRows)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions')
                AND NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                VALUES (N'Travel', @Code, @Desc);
                """, new { Code = code, Desc = desc }, cancellationToken: cancellationToken));
        }

        foreach (var role in new[] { "SUPER_ADMIN", "TENANT_ADMIN", "OWNER" })
        {
            await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
                connection, role, TravelPermissions.Phase7, cancellationToken);
        }

        var hasMenuTables = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN OBJECT_ID(N'dbo.PlatformMenus', N'U') IS NOT NULL
                         AND OBJECT_ID(N'dbo.PlatformModules', N'U') IS NOT NULL THEN 1 ELSE 0 END
            """, cancellationToken: cancellationToken)) == 1;
        if (hasMenuTables)
        {
            var menus = new (string Name, string Route, string Icon, string Perm, int Sort)[]
            {
                ("Dashboard", "/travel/dashboard", "dashboard", TravelPermissions.DashboardView, 5),
                ("Reminders", "/travel/reminders", "notifications_active", TravelPermissions.ReminderView, 125),
                ("Reports & Analytics", "/travel/reports", "analytics", TravelPermissions.ReportView, 130),
            };
            foreach (var m in menus)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE pm SET
                        pm.ModuleId = mod.Id, pm.ParentId = NULL, pm.Name = @Name, pm.Icon = @Icon,
                        pm.PermissionCode = @Perm, pm.SortOrder = @Sort, pm.IsActive = 1, pm.Visible = 1,
                        pm.DisplayName = @Name, pm.Category = N'Travel', pm.ModuleKey = @ModuleCode,
                        pm.FeatureKey = @FeatureKey, pm.UpdatedAt = SYSUTCDATETIME()
                    FROM PlatformMenus pm
                    CROSS JOIN PlatformModules mod
                    WHERE mod.ModuleKey = @PlatformModule AND pm.Route = @Route;

                    IF @@ROWCOUNT = 0
                    INSERT INTO PlatformMenus (
                        ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                        DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                    SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                           @Name, NULL, N'Travel', 1, @FeatureKey, @ModuleCode, 0, SYSUTCDATETIME()
                    FROM PlatformModules mod
                    WHERE mod.ModuleKey = @PlatformModule
                      AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route);
                    """,
                    new
                    {
                        PlatformModule = "travel-nexus", m.Name, m.Route, m.Icon, m.Perm, m.Sort,
                        ModuleCode = TenantModuleCatalog.TravelAgencyModuleCode,
                        FeatureKey = TenantModuleCatalog.TravelAgencyLegacyKey
                    }, cancellationToken: cancellationToken));
            }
        }

        logger.LogInformation("TravelNexusRemindersMigration applied (reminder rules and log, quiet hours, templates, permissions, menus).");
    }
}
