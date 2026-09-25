using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;
using System.Data;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// WhatsAppTemplates catalog + ManageAccounts / ManageTemplates permissions.
/// Seeded templates are Draft — never assumed Approved.
/// </summary>
public static class WhatsAppTemplatesMigration
{
    private static readonly string[] CatalogNames =
    [
        "demo_confirmation",
        "demo_reminder",
        "demo_followup",
        "proposal_followup",
        "booking_confirmation",
        "trip_confirmation",
        "trip_update",
        "payment_reminder",
        "support_update"
    ];

    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != ConnectionState.Open)
            connection.Open();

        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'WhatsAppTemplates', N'U') IS NULL
            BEGIN
                CREATE TABLE WhatsAppTemplates (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TenantId INT NOT NULL,
                    WhatsAppAccountId INT NOT NULL,
                    Name NVARCHAR(200) NOT NULL,
                    Language NVARCHAR(20) NOT NULL CONSTRAINT DF_WhatsAppTemplates_Language DEFAULT N'en',
                    Category NVARCHAR(40) NOT NULL CONSTRAINT DF_WhatsAppTemplates_Category DEFAULT N'UTILITY',
                    Status NVARCHAR(40) NOT NULL CONSTRAINT DF_WhatsAppTemplates_Status DEFAULT N'Draft',
                    MetaTemplateId NVARCHAR(120) NULL,
                    BodyPreview NVARCHAR(1000) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppTemplates_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_WhatsAppTemplates_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CreatedBy INT NULL,
                    UpdatedBy INT NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_WhatsAppTemplates_IsDeleted DEFAULT 0,
                    CONSTRAINT UQ_WhatsAppTemplates_Account_Name_Lang
                        UNIQUE (TenantId, WhatsAppAccountId, Name, Language)
                );
                CREATE INDEX IX_WhatsAppTemplates_Account
                    ON WhatsAppTemplates(TenantId, WhatsAppAccountId, Status);
            END
            """, cancellationToken: cancellationToken));

        // Idempotent: table may already exist from a failed earlier attempt without IsDeleted.
        await connection.ExecuteAsync(new CommandDefinition("""
            IF OBJECT_ID(N'WhatsAppTemplates', N'U') IS NOT NULL
               AND COL_LENGTH(N'WhatsAppTemplates', N'IsDeleted') IS NULL
                ALTER TABLE WhatsAppTemplates ADD IsDeleted BIT NOT NULL
                    CONSTRAINT DF_WhatsAppTemplates_IsDeleted DEFAULT 0;
            """, cancellationToken: cancellationToken));

        await SeedPermissionsAsync(connection, cancellationToken);
        await SeedCatalogAsync(connection, cancellationToken);

        logger.LogInformation("WhatsAppTemplatesMigration applied successfully.");
    }

    private static async Task SeedPermissionsAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Permissions') THEN 1 ELSE 0 END",
                cancellationToken: ct)) != 1)
            return;

        var perms = new (string Code, string Desc)[]
        {
            (WhatsAppPermissions.ManageAccounts, "Manage WhatsApp business accounts and health"),
            (WhatsAppPermissions.ManageTemplates, "Manage WhatsApp message templates"),
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
    }

    private static async Task SeedCatalogAsync(IDbConnection connection, CancellationToken ct)
    {
        if (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'WhatsAppAccounts', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct)) != 1)
            return;

        // WhatsAppAccounts has IsActive, not IsDeleted (see WhatsAppDomainFoundationMigration).
        var accounts = (await connection.QueryAsync<(int Id, int TenantId)>(new CommandDefinition("""
            SELECT Id, TenantId FROM WhatsAppAccounts WHERE IsActive = 1
            """, cancellationToken: ct))).ToList();

        foreach (var account in accounts)
        {
            foreach (var name in CatalogNames)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    IF NOT EXISTS (
                        SELECT 1 FROM WhatsAppTemplates
                        WHERE TenantId = @TenantId AND WhatsAppAccountId = @AccountId
                          AND Name = @Name AND Language = N'en' AND IsDeleted = 0)
                    BEGIN
                        INSERT INTO WhatsAppTemplates
                            (TenantId, WhatsAppAccountId, Name, Language, Category, Status, BodyPreview)
                        VALUES
                            (@TenantId, @AccountId, @Name, N'en', N'UTILITY', N'Draft',
                             N'SheikhGo catalog placeholder — approve in Meta before sending.');
                    END
                    """, new
                    {
                        TenantId = account.TenantId,
                        AccountId = account.Id,
                        Name = name
                    }, cancellationToken: ct));
            }
        }
    }
}
