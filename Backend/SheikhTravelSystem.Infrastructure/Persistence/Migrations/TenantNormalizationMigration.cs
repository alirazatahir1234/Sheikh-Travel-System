using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using System.Text.Json;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// Normalizes tenant data into dedicated tables (subscriptions, modules, branding, security, billing, GPS).
/// </summary>
public static class TenantNormalizationMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await ExtendTenantsTableAsync(connection, cancellationToken);
        await CreateModulesTableAsync(connection, cancellationToken);
        await CreateTenantModulesTableAsync(connection, cancellationToken);
        await CreateTenantSubscriptionsTableAsync(connection, cancellationToken);
        await CreateTenantBrandingTableAsync(connection, cancellationToken);
        await CreateTenantSecuritySettingsTableAsync(connection, cancellationToken);
        await CreateTenantBillingTableAsync(connection, cancellationToken);
        await CreateTenantGpsSettingsTableAsync(connection, cancellationToken);

        await SeedModulesAsync(connection, cancellationToken);
        await BackfillFromLegacyTenantsAsync(connection, cancellationToken);

        logger.LogInformation("Tenant normalization migration completed.");
    }

    private static async Task ExtendTenantsTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await AddColumnIfMissingAsync(connection, "Tenants", "TenantType", "NVARCHAR(100) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Tenants", "IndustryType", "NVARCHAR(100) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Tenants", "StorageModel", "NVARCHAR(50) NOT NULL DEFAULT N'SharedDatabase'", ct);
        await AddColumnIfMissingAsync(connection, "Tenants", "Status", "NVARCHAR(50) NOT NULL DEFAULT N'Active'", ct);
        await AddColumnIfMissingAsync(connection, "Tenants", "DataRegion", "NVARCHAR(100) NULL", ct);
        await AddColumnIfMissingAsync(connection, "Tenants", "CreatedByUserId", "INT NULL", ct);

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Tenants SET TenantType = N'Travel Agency' WHERE TenantType IS NULL;
            UPDATE Tenants SET IndustryType = N'Logistics & Transport' WHERE IndustryType IS NULL;
            UPDATE Tenants SET StorageModel = N'SharedDatabase' WHERE StorageModel IS NULL OR LTRIM(RTRIM(StorageModel)) = '';
            UPDATE Tenants SET Status = CASE WHEN IsActive = 1 THEN N'Active' ELSE N'Suspended' END
            WHERE Status IS NULL OR LTRIM(RTRIM(Status)) = '';
            UPDATE Tenants SET DataRegion = N'UAE' WHERE DataRegion IS NULL;
            """, cancellationToken: ct));
    }

    private static async Task CreateModulesTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Modules')
            CREATE TABLE Modules (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                ModuleCode NVARCHAR(100) NOT NULL,
                ModuleName NVARCHAR(100) NOT NULL,
                LegacyKeysJson NVARCHAR(MAX) NULL,
                CONSTRAINT UQ_Modules_Code UNIQUE (ModuleCode)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateTenantModulesTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantModules')
            CREATE TABLE TenantModules (
                TenantId INT NOT NULL,
                ModuleId INT NOT NULL,
                EnabledAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT PK_TenantModules PRIMARY KEY (TenantId, ModuleId),
                CONSTRAINT FK_TenantModules_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT FK_TenantModules_Modules FOREIGN KEY (ModuleId) REFERENCES Modules(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateTenantSubscriptionsTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantSubscriptions')
            CREATE TABLE TenantSubscriptions (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TenantId INT NOT NULL,
                PlanName NVARCHAR(100) NULL,
                MaxUsers INT NULL,
                MaxVehicles INT NULL,
                MaxDrivers INT NULL,
                MaxBranches INT NULL,
                MaxGpsDevices INT NULL,
                Status NVARCHAR(50) NOT NULL DEFAULT N'Active',
                TrialStartDate DATETIME2 NULL,
                TrialEndDate DATETIME2 NULL,
                SubscriptionStartDate DATETIME2 NULL,
                SubscriptionEndDate DATETIME2 NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                UpdatedAt DATETIME2 NULL,
                CONSTRAINT UQ_TenantSubscriptions_TenantId UNIQUE (TenantId),
                CONSTRAINT FK_TenantSubscriptions_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateTenantBrandingTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantBranding')
            CREATE TABLE TenantBranding (
                TenantId INT NOT NULL PRIMARY KEY,
                LogoUrl NVARCHAR(500) NULL,
                PrimaryColor NVARCHAR(20) NULL,
                Website NVARCHAR(300) NULL,
                SupportEmail NVARCHAR(300) NULL,
                Country NVARCHAR(100) NULL,
                CurrencyCode NVARCHAR(20) NULL,
                TimeZone NVARCHAR(100) NULL,
                CONSTRAINT FK_TenantBranding_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateTenantSecuritySettingsTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantSecuritySettings')
            CREATE TABLE TenantSecuritySettings (
                TenantId INT NOT NULL PRIMARY KEY,
                IsMfaRequired BIT NOT NULL DEFAULT 0,
                PasswordExpiryDays INT NULL,
                SessionTimeoutMinutes INT NULL,
                IsGdprEnabled BIT NOT NULL DEFAULT 1,
                IsAuditLoggingEnabled BIT NOT NULL DEFAULT 1,
                IsVatEnabled BIT NOT NULL DEFAULT 0,
                CONSTRAINT FK_TenantSecuritySettings_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateTenantBillingTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantBilling')
            CREATE TABLE TenantBilling (
                TenantId INT NOT NULL PRIMARY KEY,
                BillingContactName NVARCHAR(200) NULL,
                BillingEmail NVARCHAR(200) NULL,
                BillingAddress NVARCHAR(500) NULL,
                CompanyTRN NVARCHAR(100) NULL,
                CONSTRAINT FK_TenantBilling_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task CreateTenantGpsSettingsTableAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TenantGpsSettings')
            CREATE TABLE TenantGpsSettings (
                TenantId INT NOT NULL PRIMARY KEY,
                ProviderName NVARCHAR(100) NULL,
                ApiEndpoint NVARCHAR(500) NULL,
                ApiKey NVARCHAR(MAX) NULL,
                CONSTRAINT FK_TenantGpsSettings_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            """, cancellationToken: ct));
    }

    private static async Task SeedModulesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        foreach (var module in TenantModuleCatalog.All)
        {
            var legacyJson = JsonSerializer.Serialize(module.LegacyKeys);
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM Modules WHERE ModuleCode = @Code)
                INSERT INTO Modules (ModuleCode, ModuleName, LegacyKeysJson)
                VALUES (@Code, @Name, @LegacyJson);
                """, new { Code = module.Code, Name = module.Name, LegacyJson = legacyJson },
                cancellationToken: ct));
        }
    }

    private static async Task BackfillFromLegacyTenantsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var tenants = (await connection.QueryAsync<(int Id, string? LogoUrl, string? PrimaryColor, string? SubscriptionPlan, string? EnabledModulesJson)>(
            new CommandDefinition(
                "SELECT Id, LogoUrl, PrimaryColor, SubscriptionPlan, EnabledModulesJson FROM Tenants",
                cancellationToken: ct))).ToList();

        foreach (var tenant in tenants)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM TenantBranding WHERE TenantId = @TenantId)
                INSERT INTO TenantBranding (TenantId, LogoUrl, PrimaryColor, Country, CurrencyCode, TimeZone)
                VALUES (@TenantId, @LogoUrl, @PrimaryColor, N'Pakistan', N'PKR', N'Asia/Karachi');
                """, new { TenantId = tenant.Id, tenant.LogoUrl, tenant.PrimaryColor },
                cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM TenantSecuritySettings WHERE TenantId = @TenantId)
                INSERT INTO TenantSecuritySettings (TenantId, IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes, IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled)
                VALUES (@TenantId, 0, 90, 30, 1, 1, 0);
                """, new { TenantId = tenant.Id }, cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition("""
                IF NOT EXISTS (SELECT 1 FROM TenantSubscriptions WHERE TenantId = @TenantId)
                INSERT INTO TenantSubscriptions (TenantId, PlanName, Status, SubscriptionStartDate, MaxUsers, MaxVehicles, MaxDrivers, MaxBranches, MaxGpsDevices)
                VALUES (@TenantId, COALESCE(@Plan, N'Enterprise'), N'Active', GETUTCDATE(), 100, 500, 500, 50, 500);
                """, new { TenantId = tenant.Id, Plan = tenant.SubscriptionPlan }, cancellationToken: ct));

            var legacyKeys = ParseLegacyKeys(tenant.EnabledModulesJson);
            var moduleCodes = legacyKeys.Count > 0
                ? TenantModuleCatalog.CodesFromLegacyKeys(legacyKeys)
                : TenantModuleCatalog.DefaultModuleCodes;

            foreach (var code in moduleCodes)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    IF NOT EXISTS (
                        SELECT 1 FROM TenantModules tm
                        INNER JOIN Modules m ON m.Id = tm.ModuleId
                        WHERE tm.TenantId = @TenantId AND m.ModuleCode = @Code
                    )
                    INSERT INTO TenantModules (TenantId, ModuleId)
                    SELECT @TenantId, m.Id FROM Modules m WHERE m.ModuleCode = @Code;
                    """, new { TenantId = tenant.Id, Code = code }, cancellationToken: ct));
            }

            if (legacyKeys.Count == 0 && moduleCodes.Count > 0)
            {
                var synced = TenantModuleCatalog.LegacyKeysFromCodes(moduleCodes);
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE Tenants SET EnabledModulesJson = @Json WHERE Id = @TenantId",
                    new { TenantId = tenant.Id, Json = TenantModuleCatalog.SerializeLegacyKeys(synced) },
                    cancellationToken: ct));
            }
        }
    }

    private static IReadOnlyList<string> ParseLegacyKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static async Task AddColumnIfMissingAsync(
        System.Data.IDbConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken ct)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @Table AND COLUMN_NAME = @Column",
            new { Table = table, Column = column },
            cancellationToken: ct));

        if (exists == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                $"ALTER TABLE [{table}] ADD [{column}] {definition}",
                cancellationToken: ct));
        }
    }
}
