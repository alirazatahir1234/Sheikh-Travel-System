using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Creates the generic per-tenant PlatformSettings key/value store that backs the
/// Settings control panel, and seeds default General category values for existing tenants.
/// </summary>
public static class PlatformSettingsMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await CreatePlatformSettingsTableAsync(connection, cancellationToken);
        await SeedDefaultGeneralSettingsAsync(connection, cancellationToken);

        logger.LogInformation("Platform settings migration completed.");
    }

    private static async Task CreatePlatformSettingsTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PlatformSettings')
            CREATE TABLE PlatformSettings (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                Category NVARCHAR(64) NOT NULL,
                [Key] NVARCHAR(128) NOT NULL,
                Value NVARCHAR(MAX) NULL,
                DataType NVARCHAR(32) NOT NULL DEFAULT N'string',
                Description NVARCHAR(512) NULL,
                IsEncrypted BIT NOT NULL DEFAULT 0,
                IsSystem BIT NOT NULL DEFAULT 0,
                IsActive BIT NOT NULL DEFAULT 1,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CONSTRAINT FK_PlatformSettings_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT UQ_PlatformSettings_Tenant_Category_Key UNIQUE (TenantId, Category, [Key])
            );
            """, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = 'IX_PlatformSettings_Tenant_Category'
                  AND object_id = OBJECT_ID('PlatformSettings')
            )
            CREATE INDEX IX_PlatformSettings_Tenant_Category ON PlatformSettings (TenantId, Category);
            """, cancellationToken: ct));
    }

    private static async Task SeedDefaultGeneralSettingsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var tenants = (await connection.QueryAsync<(int Id, string Name)>(
            new CommandDefinition("SELECT Id, Name FROM Tenants", cancellationToken: ct))).ToList();

        foreach (var tenant in tenants)
        {
            await UpsertSettingIfMissingAsync(connection, tenant.Id, "General", "CompanyName", tenant.Name, "string", ct);
            await UpsertSettingIfMissingAsync(connection, tenant.Id, "General", "DefaultCurrency", "AED", "string", ct);
            await UpsertSettingIfMissingAsync(connection, tenant.Id, "General", "Timezone", "Asia/Dubai", "string", ct);
            await UpsertSettingIfMissingAsync(connection, tenant.Id, "General", "Language", "en-AE", "string", ct);
        }
    }

    private static async Task UpsertSettingIfMissingAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        string category,
        string key,
        string? value,
        string dataType,
        CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM PlatformSettings WHERE TenantId = @TenantId AND Category = @Category AND [Key] = @Key)
            INSERT INTO PlatformSettings (TenantId, Category, [Key], Value, DataType)
            VALUES (@TenantId, @Category, @Key, @Value, @DataType);
            """, new { TenantId = tenantId, Category = category, Key = key, Value = value, DataType = dataType },
            cancellationToken: ct));
    }
}
