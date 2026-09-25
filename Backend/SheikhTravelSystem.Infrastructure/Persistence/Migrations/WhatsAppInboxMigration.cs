using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// WhatsApp Cloud API inbox tables, UAE/PK account seeds, permissions, and menu.
/// </summary>
public static class WhatsAppInboxMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await CreateTablesAsync(connection, cancellationToken);
        await SeedAccountsAsync(connection, cancellationToken);
        await SeedPermissionsAndMenusAsync(connection, cancellationToken);
        logger.LogInformation("WhatsAppInboxMigration applied successfully.");
    }

    private static async Task CreateTablesAsync(IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'WhatsAppAccounts', N'U') IS NULL
            BEGIN
                CREATE TABLE WhatsAppAccounts (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL CONSTRAINT DF_WhatsAppAccounts_TenantId DEFAULT 1,
                    Code NVARCHAR(20) NOT NULL,
                    DisplayName NVARCHAR(120) NOT NULL,
                    E164Phone NVARCHAR(32) NOT NULL,
                    Purpose NVARCHAR(40) NOT NULL,
                    PhoneNumberId NVARCHAR(64) NULL,
                    WabaId NVARCHAR(64) NULL,
                    IsActive BIT NOT NULL CONSTRAINT DF_WhatsAppAccounts_Active DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppAccounts_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppAccounts_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
                CREATE UNIQUE INDEX IX_WhatsAppAccounts_Tenant_Code ON WhatsAppAccounts(TenantId, Code);
                CREATE INDEX IX_WhatsAppAccounts_PhoneNumberId ON WhatsAppAccounts(PhoneNumberId) WHERE PhoneNumberId IS NOT NULL;
            END

            IF OBJECT_ID(N'WhatsAppContacts', N'U') IS NULL
            BEGIN
                CREATE TABLE WhatsAppContacts (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL,
                    AccountId INT NOT NULL,
                    WaId NVARCHAR(64) NOT NULL,
                    PhoneE164 NVARCHAR(32) NOT NULL,
                    ProfileName NVARCHAR(200) NULL,
                    CustomerId INT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppContacts_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppContacts_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_WhatsAppContacts_Account FOREIGN KEY (AccountId) REFERENCES WhatsAppAccounts(Id)
                );
                CREATE UNIQUE INDEX IX_WhatsAppContacts_Account_WaId ON WhatsAppContacts(TenantId, AccountId, WaId);
                CREATE INDEX IX_WhatsAppContacts_Customer ON WhatsAppContacts(TenantId, CustomerId) WHERE CustomerId IS NOT NULL;
            END

            IF OBJECT_ID(N'WhatsAppConversations', N'U') IS NULL
            BEGIN
                CREATE TABLE WhatsAppConversations (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL,
                    AccountId INT NOT NULL,
                    ContactId INT NOT NULL,
                    Status NVARCHAR(40) NOT NULL CONSTRAINT DF_WhatsAppConversations_Status DEFAULT N'Open',
                    LastMessageAt DATETIME2 NULL,
                    LastMessagePreview NVARCHAR(500) NULL,
                    UnreadCount INT NOT NULL CONSTRAINT DF_WhatsAppConversations_Unread DEFAULT 0,
                    AssignedUserId INT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppConversations_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppConversations_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_WhatsAppConversations_Account FOREIGN KEY (AccountId) REFERENCES WhatsAppAccounts(Id),
                    CONSTRAINT FK_WhatsAppConversations_Contact FOREIGN KEY (ContactId) REFERENCES WhatsAppContacts(Id)
                );
                CREATE UNIQUE INDEX IX_WhatsAppConversations_Account_Contact ON WhatsAppConversations(TenantId, AccountId, ContactId);
                CREATE INDEX IX_WhatsAppConversations_LastMsg ON WhatsAppConversations(TenantId, AccountId, LastMessageAt DESC);
            END

            IF OBJECT_ID(N'WhatsAppMessages', N'U') IS NULL
            BEGIN
                CREATE TABLE WhatsAppMessages (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL,
                    ConversationId INT NOT NULL,
                    Direction NVARCHAR(16) NOT NULL,
                    MetaMessageId NVARCHAR(128) NULL,
                    Type NVARCHAR(40) NOT NULL CONSTRAINT DF_WhatsAppMessages_Type DEFAULT N'text',
                    Body NVARCHAR(MAX) NULL,
                    MediaId NVARCHAR(128) NULL,
                    Status NVARCHAR(40) NOT NULL,
                    ErrorMessage NVARCHAR(1000) NULL,
                    RawJson NVARCHAR(MAX) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppMessages_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_WhatsAppMessages_Conversation FOREIGN KEY (ConversationId) REFERENCES WhatsAppConversations(Id)
                );
                CREATE UNIQUE INDEX IX_WhatsAppMessages_MetaId ON WhatsAppMessages(MetaMessageId) WHERE MetaMessageId IS NOT NULL;
                CREATE INDEX IX_WhatsAppMessages_Conversation ON WhatsAppMessages(ConversationId, CreatedAt);
            END

            IF OBJECT_ID(N'WhatsAppWebhookDeadLetters', N'U') IS NULL
            BEGIN
                CREATE TABLE WhatsAppWebhookDeadLetters (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Reason NVARCHAR(400) NOT NULL,
                    PayloadPreview NVARCHAR(2000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppDeadLetters_CreatedAt DEFAULT SYSUTCDATETIME()
                );
            END
            """, cancellationToken: ct));
    }

    private static async Task SeedAccountsAsync(IDbConnection connection, CancellationToken ct)
    {
        // Legacy column names only — SQL Server validates INSERT column lists even inside IF branches.
        // WhatsAppDomainFoundationMigration copies DisplayName/E164Phone → Name/DisplayPhoneNumber.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF COL_LENGTH(N'WhatsAppAccounts', N'DisplayName') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM WhatsAppAccounts WHERE TenantId = 1 AND Code = N'UAE')
                    INSERT INTO WhatsAppAccounts (TenantId, Code, DisplayName, E164Phone, Purpose, IsActive)
                    VALUES (1, N'UAE', N'SheikhGo UAE', N'+971557701219', N'Sales', 1);

                IF NOT EXISTS (SELECT 1 FROM WhatsAppAccounts WHERE TenantId = 1 AND Code = N'PK')
                    INSERT INTO WhatsAppAccounts (TenantId, Code, DisplayName, E164Phone, Purpose, IsActive)
                    VALUES (1, N'PK', N'SheikhGo Pakistan', N'+923177368305', N'Support', 1);
            END
            ELSE IF COL_LENGTH(N'WhatsAppAccounts', N'Name') IS NOT NULL
            BEGIN
                DECLARE @sql NVARCHAR(MAX) = N'
                    IF NOT EXISTS (SELECT 1 FROM WhatsAppAccounts WHERE TenantId = 1 AND Code = N''UAE'')
                        INSERT INTO WhatsAppAccounts (TenantId, Code, Name, DisplayPhoneNumber, Purpose, IsActive)
                        VALUES (1, N''UAE'', N''SheikhGo UAE'', N''+971557701219'', N''Sales'', 1);
                    IF NOT EXISTS (SELECT 1 FROM WhatsAppAccounts WHERE TenantId = 1 AND Code = N''PK'')
                        INSERT INTO WhatsAppAccounts (TenantId, Code, Name, DisplayPhoneNumber, Purpose, IsActive)
                        VALUES (1, N''PK'', N''SheikhGo Pakistan'', N''+923177368305'', N''Support'', 1);';
                EXEC sp_executesql @sql;
            END
            """, cancellationToken: ct));
    }

    private static async Task SeedPermissionsAndMenusAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: ct)) != 1)
            return;

        var perms = new (string Code, string Desc)[]
        {
            (WhatsAppPermissions.View, "View WhatsApp inbox"),
            (WhatsAppPermissions.Reply, "Reply on WhatsApp conversations"),
            (WhatsAppPermissions.Manage, "Manage WhatsApp accounts and customer links"),
        };

        foreach (var (code, desc) in perms)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionCode = @Code)
                    INSERT INTO Permissions (ModuleName, PermissionCode, Description)
                    VALUES (N'WhatsApp', @Code, @Desc);
                """, new { Code = code, Desc = desc }, cancellationToken: ct));
        }

        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "SUPER_ADMIN", WhatsAppPermissions.All, ct);
        await TenantRolePermissionSeeder.AssignRolePermissionsForAllTenantsAsync(
            connection, "TENANT_ADMIN", WhatsAppPermissions.All, ct);

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformModules')
            AND NOT EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'whatsapp')
            BEGIN
                INSERT INTO PlatformModules (Name, ModuleKey, Icon, SortOrder, IsCollapsible)
                VALUES (N'WhatsApp', N'whatsapp', N'chat', 55, 0);
            END
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM PlatformModules WHERE ModuleKey = N'whatsapp')
            AND NOT EXISTS (SELECT 1 FROM PlatformMenus WHERE Route = N'/whatsapp')
            BEGIN
                INSERT INTO PlatformMenus (ModuleId, ParentId, Name, Route, Icon, PermissionCode, SortOrder, IsActive,
                    DisplayName, Description, Category, Visible, FeatureKey, ModuleKey, IsMobileSupported, UpdatedAt)
                SELECT mod.Id, NULL, N'WhatsApp Inbox', N'/whatsapp', N'chat', @Perm, 10, 1,
                       N'WhatsApp Inbox', N'Multi-number WhatsApp Business inbox', N'Communications', 1,
                       N'whatsapp', N'whatsapp', 0, SYSUTCDATETIME()
                FROM PlatformModules mod WHERE mod.ModuleKey = N'whatsapp';
            END
            """, new { Perm = WhatsAppPermissions.View }, cancellationToken: ct));
    }
}
