using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Consolidates WhatsApp nav under one collapsible PlatformModules parent (WhatsApp)
/// with Inbox, Accounts, Templates, Automations (+ Webhooks). Routes unchanged.
/// </summary>
public static class WhatsAppMenuConsolidationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformMenus'
                ) AND EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformModules'
                ) THEN 1 ELSE 0 END
                """,
                cancellationToken: cancellationToken)) != 1)
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'whatsapp')
            BEGIN
                INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
                VALUES (N'WhatsApp', N'whatsapp', N'chat', 55, 1);
            END

            UPDATE PlatformModules
            SET Name = N'WhatsApp',
                Icon = N'chat',
                SortOrder = CASE WHEN SortOrder IS NULL OR SortOrder = 0 THEN 55 ELSE SortOrder END,
                IsCollapsible = 1,
                DisplayName = COALESCE(DisplayName, N'WhatsApp'),
                Visible = 1
            WHERE ModuleKey = N'whatsapp';
            """, cancellationToken: cancellationToken));

        var menus = new (string Name, string Route, string Icon, string Perm, int Sort, string Display, string Desc)[]
        {
            ("WhatsApp Inbox", "/whatsapp", "chat", WhatsAppPermissions.View, 10, "Inbox",
                "Multi-number WhatsApp Business inbox"),
            ("WhatsApp Accounts", "/whatsapp/accounts", "phonelink_setup", WhatsAppPermissions.ManageAccounts, 20, "Accounts",
                "WhatsApp Business account health and controls"),
            ("WhatsApp Templates", "/whatsapp/templates", "description", WhatsAppPermissions.ManageTemplates, 30, "Templates",
                "Message template catalog and approval status"),
            ("WhatsApp Automations", "/whatsapp/automations", "auto_awesome", WhatsAppPermissions.Manage, 40, "Automations",
                "Booking and trip WhatsApp automation rules"),
            ("WhatsApp Webhooks", "/whatsapp/webhook-logs", "receipt_long", WhatsAppPermissions.Manage, 50, "Webhooks",
                "Inbound webhook delivery logs"),
        };

        foreach (var m in menus)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                -- Move any existing row for this route onto the WhatsApp module and refresh labels.
                UPDATE pm SET
                    pm.ModuleId = mod.Id,
                    pm.ParentId = NULL,
                    pm.Name = @Name,
                    pm.Icon = @Icon,
                    pm.PermissionCode = @Perm,
                    pm.SortOrder = @Sort,
                    pm.IsActive = 1,
                    pm.Visible = 1,
                    pm.DisplayName = @Display,
                    pm.Description = @Desc,
                    pm.Category = N'Communications',
                    pm.FeatureKey = N'whatsapp',
                    pm.ModuleKey = N'whatsapp',
                    pm.UpdatedAt = SYSUTCDATETIME()
                FROM PlatformMenus pm
                CROSS JOIN PlatformModules mod
                WHERE mod.ModuleKey = N'whatsapp'
                  AND pm.Route = @Route;

                IF @@ROWCOUNT = 0
                INSERT INTO PlatformMenus (
                    ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                    DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                SELECT mod.Id, NULL, @Name, @Route, @Icon, @Perm, @Sort, 1,
                       @Display, @Desc, N'Communications', 1, N'whatsapp', N'whatsapp', 0, SYSUTCDATETIME()
                FROM PlatformModules mod
                WHERE mod.ModuleKey = N'whatsapp'
                  AND NOT EXISTS (SELECT 1 FROM PlatformMenus x WHERE x.Route = @Route);
                """,
                new { m.Name, m.Route, m.Icon, m.Perm, m.Sort, m.Display, m.Desc },
                cancellationToken: cancellationToken));
        }

        logger.LogInformation(
            "WhatsAppMenuConsolidationMigration applied (WhatsApp → Inbox, Accounts, Templates, Automations, Webhooks).");
    }
}
