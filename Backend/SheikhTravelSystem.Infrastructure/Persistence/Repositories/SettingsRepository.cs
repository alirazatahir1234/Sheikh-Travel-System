using System.Globalization;
using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Settings;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class SettingsRepository(IDbConnectionFactory dbFactory) : ISettingsRepository
{
    private static readonly HashSet<string> SecurityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "IsMfaRequired", "PasswordExpiryDays", "SessionTimeoutMinutes",
        "IsGdprEnabled", "IsAuditLoggingEnabled", "IsVatEnabled"
    };

    private static readonly HashSet<string> BrandingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "LogoUrl", "PrimaryColor", "Website", "SupportEmail", "Country", "CurrencyCode", "TimeZone"
    };

    public async Task<Dictionary<string, string?>> GetByCategoryAsync(
        int tenantId,
        string category,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var values = await LoadGenericAsync(connection, tenantId, category, cancellationToken);

        if (string.Equals(category, SettingsCategories.Security, StringComparison.OrdinalIgnoreCase))
            await OverlaySecurityAsync(connection, tenantId, values, cancellationToken);
        else if (string.Equals(category, SettingsCategories.Branding, StringComparison.OrdinalIgnoreCase))
            await OverlayBrandingAsync(connection, tenantId, values, cancellationToken);

        return values;
    }

    public async Task SaveByCategoryAsync(
        int tenantId,
        string category,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var isSecurity = string.Equals(category, SettingsCategories.Security, StringComparison.OrdinalIgnoreCase);
        var isBranding = string.Equals(category, SettingsCategories.Branding, StringComparison.OrdinalIgnoreCase);

        var tableManagedKeys = isSecurity
            ? SecurityKeys
            : isBranding
                ? BrandingKeys
                : null;

        var genericValues = tableManagedKeys is null
            ? values
            : values.Where(kv => !tableManagedKeys.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        await SaveGenericAsync(connection, tenantId, category, genericValues, cancellationToken);

        if (isSecurity)
            await SaveSecurityAsync(connection, tenantId, values, cancellationToken);
        else if (isBranding)
            await SaveBrandingAsync(connection, tenantId, values, cancellationToken);
    }

    private static async Task<Dictionary<string, string?>> LoadGenericAsync(
        System.Data.IDbConnection connection, int tenantId, string category, CancellationToken ct)
    {
        var rows = await connection.QueryAsync<(string Key, string? Value)>(new CommandDefinition("""
            SELECT [Key], Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = @Category AND IsActive = 1
            """, new { TenantId = tenantId, Category = category }, cancellationToken: ct));

        return rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task SaveGenericAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        string category,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken ct)
    {
        foreach (var (key, value) in values)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM PlatformSettings WHERE TenantId = @TenantId AND Category = @Category AND [Key] = @Key)
                    UPDATE PlatformSettings SET Value = @Value, UpdatedAt = GETUTCDATE()
                    WHERE TenantId = @TenantId AND Category = @Category AND [Key] = @Key;
                ELSE
                    INSERT INTO PlatformSettings (TenantId, Category, [Key], Value)
                    VALUES (@TenantId, @Category, @Key, @Value);
                """, new { TenantId = tenantId, Category = category, Key = key, Value = value },
                cancellationToken: ct));
        }
    }

    private static async Task OverlaySecurityAsync(
        System.Data.IDbConnection connection, int tenantId, Dictionary<string, string?> target, CancellationToken ct)
    {
        var row = await connection.QuerySingleOrDefaultAsync<(bool IsMfaRequired, int? PasswordExpiryDays,
            int? SessionTimeoutMinutes, bool IsGdprEnabled, bool IsAuditLoggingEnabled, bool IsVatEnabled)?>(
            new CommandDefinition("""
                SELECT IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes,
                       IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled
                FROM TenantSecuritySettings WHERE TenantId = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: ct));

        var s = row ?? (false, 90, 30, true, true, false);

        target["IsMfaRequired"] = Bool(s.IsMfaRequired);
        target["PasswordExpiryDays"] = Int(s.PasswordExpiryDays);
        target["SessionTimeoutMinutes"] = Int(s.SessionTimeoutMinutes);
        target["IsGdprEnabled"] = Bool(s.IsGdprEnabled);
        target["IsAuditLoggingEnabled"] = Bool(s.IsAuditLoggingEnabled);
        target["IsVatEnabled"] = Bool(s.IsVatEnabled);
    }

    private static async Task SaveSecurityAsync(
        System.Data.IDbConnection connection, int tenantId,
        IReadOnlyDictionary<string, string?> values, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM TenantSecuritySettings WHERE TenantId = @TenantId)
                UPDATE TenantSecuritySettings SET
                    IsMfaRequired = @IsMfaRequired,
                    PasswordExpiryDays = @PasswordExpiryDays,
                    SessionTimeoutMinutes = @SessionTimeoutMinutes,
                    IsGdprEnabled = @IsGdprEnabled,
                    IsAuditLoggingEnabled = @IsAuditLoggingEnabled,
                    IsVatEnabled = @IsVatEnabled
                WHERE TenantId = @TenantId;
            ELSE
                INSERT INTO TenantSecuritySettings (TenantId, IsMfaRequired, PasswordExpiryDays, SessionTimeoutMinutes,
                    IsGdprEnabled, IsAuditLoggingEnabled, IsVatEnabled)
                VALUES (@TenantId, @IsMfaRequired, @PasswordExpiryDays, @SessionTimeoutMinutes,
                    @IsGdprEnabled, @IsAuditLoggingEnabled, @IsVatEnabled);
            """, new
        {
            TenantId = tenantId,
            IsMfaRequired = ParseBool(values, "IsMfaRequired"),
            PasswordExpiryDays = ParseInt(values, "PasswordExpiryDays"),
            SessionTimeoutMinutes = ParseInt(values, "SessionTimeoutMinutes"),
            IsGdprEnabled = ParseBool(values, "IsGdprEnabled"),
            IsAuditLoggingEnabled = ParseBool(values, "IsAuditLoggingEnabled"),
            IsVatEnabled = ParseBool(values, "IsVatEnabled"),
        }, cancellationToken: ct));
    }

    private static async Task OverlayBrandingAsync(
        System.Data.IDbConnection connection, int tenantId, Dictionary<string, string?> target, CancellationToken ct)
    {
        var row = await connection.QuerySingleOrDefaultAsync<(string? LogoUrl, string? PrimaryColor, string? Website,
            string? SupportEmail, string? Country, string? CurrencyCode, string? TimeZone)?>(
            new CommandDefinition("""
                SELECT LogoUrl, PrimaryColor, Website, SupportEmail, Country, CurrencyCode, TimeZone
                FROM TenantBranding WHERE TenantId = @TenantId
                """, new { TenantId = tenantId }, cancellationToken: ct));

        if (row is null) return;
        var b = row.Value;

        target["LogoUrl"] = b.LogoUrl;
        target["PrimaryColor"] = b.PrimaryColor;
        target["Website"] = b.Website;
        target["SupportEmail"] = b.SupportEmail;
        target["Country"] = b.Country;
        target["CurrencyCode"] = b.CurrencyCode;
        target["TimeZone"] = b.TimeZone;
    }

    private static async Task SaveBrandingAsync(
        System.Data.IDbConnection connection, int tenantId,
        IReadOnlyDictionary<string, string?> values, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF EXISTS (SELECT 1 FROM TenantBranding WHERE TenantId = @TenantId)
                UPDATE TenantBranding SET
                    LogoUrl = @LogoUrl,
                    PrimaryColor = @PrimaryColor,
                    Website = @Website,
                    SupportEmail = @SupportEmail,
                    Country = @Country,
                    CurrencyCode = @CurrencyCode,
                    TimeZone = @TimeZone
                WHERE TenantId = @TenantId;
            ELSE
                INSERT INTO TenantBranding (TenantId, LogoUrl, PrimaryColor, Website, SupportEmail, Country, CurrencyCode, TimeZone)
                VALUES (@TenantId, @LogoUrl, @PrimaryColor, @Website, @SupportEmail, @Country, @CurrencyCode, @TimeZone);
            """, new
        {
            TenantId = tenantId,
            LogoUrl = Get(values, "LogoUrl"),
            PrimaryColor = Get(values, "PrimaryColor"),
            Website = Get(values, "Website"),
            SupportEmail = Get(values, "SupportEmail"),
            Country = Get(values, "Country"),
            CurrencyCode = Get(values, "CurrencyCode"),
            TimeZone = Get(values, "TimeZone"),
        }, cancellationToken: ct));
    }

    private static string Bool(bool value) => value ? "true" : "false";
    private static string? Int(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string? Get(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var v) ? v : null;

    private static bool ParseBool(IReadOnlyDictionary<string, string?> values, string key) =>
        bool.TryParse(Get(values, key), out var v) && v;

    private static int? ParseInt(IReadOnlyDictionary<string, string?> values, string key) =>
        int.TryParse(Get(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
