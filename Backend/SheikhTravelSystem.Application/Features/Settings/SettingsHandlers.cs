using System.Globalization;
using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Settings;

/// <summary>
/// Central catalog of settings categories. Categories flagged not-implemented render as
/// "coming soon" placeholders in the UI but keep the navigation Figma-complete.
/// </summary>
public static class SettingsCategories
{
    public const string General = "General";
    public const string Tenant = "Tenant";
    public const string Localization = "Localization";
    public const string Security = "Security";
    public const string Notifications = "Notifications";
    public const string Documents = "Documents";
    public const string Workflows = "Workflows";
    public const string Numbering = "Numbering";
    public const string FileManagement = "FileManagement";
    public const string Branding = "Branding";
    public const string System = "System";
    public const string Integrations = "Integrations";
    public const string Audit = "Audit";
    public const string Features = "Features";
    public const string AI = "AI";

    public static readonly IReadOnlyList<SettingsCategoryDto> All =
    [
        new(General, "General", "tune", "Company identity, contact details and regional defaults.", true),
        new(Tenant, "Tenant", "apartment", "Subscription plan, limits and tenant status.", true),
        new(Localization, "Localization", "language", "Language, direction, region and number formats.", true),
        new(Security, "Security", "security", "Authentication, API security, IP management and compliance.", true),
        new(Notifications, "Notifications", "notifications", "Email, SMS, WhatsApp, push and alert preferences.", true),
        new(Documents, "Documents", "description", "Upload limits, extensions and document lifecycle.", true),
        new(Workflows, "Workflows", "account_tree", "Approval levels, auto-approval and escalation.", true),
        new(Numbering, "Numbering", "tag", "Prefixes and sequences for records.", true),
        new(FileManagement, "File Management", "folder", "Storage provider, file limits and retention.", true),
        new(Branding, "Branding", "palette", "Logo, theme colors and public contact details.", true),
        new(System, "System Preferences", "settings", "Landing page, pagination, theme and auto-save.", true),
        new(Integrations, "Integrations", "extension", "Maps, payments, GPS and government APIs.", true),
        new(Audit, "Audit & Logging", "history", "Audit logs, activity tracking and retention.", true),
        new(Features, "Feature Management", "toggle_on", "Enable or disable platform modules.", true),
        new(AI, "AI", "smart_toy", "AI provider, keys and assistant capabilities.", true),
    ];

    public static bool IsKnown(string category) =>
        All.Any(c => string.Equals(c.Id, category, StringComparison.OrdinalIgnoreCase));

    public static bool IsImplemented(string category) =>
        All.Any(c => string.Equals(c.Id, category, StringComparison.OrdinalIgnoreCase) && c.IsImplemented);
}

public class GetSettingsCategoriesQueryHandler
    : IRequestHandler<GetSettingsCategoriesQuery, ApiResponse<IReadOnlyList<SettingsCategoryDto>>>
{
    public Task<ApiResponse<IReadOnlyList<SettingsCategoryDto>>> Handle(
        GetSettingsCategoriesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(ApiResponse<IReadOnlyList<SettingsCategoryDto>>.SuccessResponse(SettingsCategories.All));
}

public class GetSettingsByCategoryQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetSettingsByCategoryQuery, ApiResponse<IReadOnlyDictionary<string, string?>>>
{
    public async Task<ApiResponse<IReadOnlyDictionary<string, string?>>> Handle(
        GetSettingsByCategoryQuery request, CancellationToken cancellationToken)
    {
        if (!SettingsCategories.IsImplemented(request.Category))
            return ApiResponse<IReadOnlyDictionary<string, string?>>.FailResponse(
                $"Settings category '{request.Category}' is not available yet.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var values = await LoadGenericAsync(connection, tenantId, request.Category, cancellationToken);

        // Overlay dedicated-table values (source of truth for their known keys).
        if (string.Equals(request.Category, SettingsCategories.Security, StringComparison.OrdinalIgnoreCase))
            await SettingsCategoryAdapters.OverlaySecurityAsync(connection, tenantId, values, cancellationToken);
        else if (string.Equals(request.Category, SettingsCategories.Branding, StringComparison.OrdinalIgnoreCase))
            await SettingsCategoryAdapters.OverlayBrandingAsync(connection, tenantId, values, cancellationToken);

        return ApiResponse<IReadOnlyDictionary<string, string?>>.SuccessResponse(values);
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
}

public class UpdateSettingsCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UpdateSettingsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!SettingsCategories.IsImplemented(request.Category))
            return ApiResponse<bool>.FailResponse(
                $"Settings category '{request.Category}' is not available yet.");

        var tenantId = tenantContext.GetRequiredTenantId();
        using var connection = dbFactory.CreateConnection();

        var isSecurity = string.Equals(request.Category, SettingsCategories.Security, StringComparison.OrdinalIgnoreCase);
        var isBranding = string.Equals(request.Category, SettingsCategories.Branding, StringComparison.OrdinalIgnoreCase);

        // Keys backed by a dedicated table are written there, not duplicated in the generic store.
        var tableManagedKeys = isSecurity
            ? SettingsCategoryAdapters.SecurityKeys
            : isBranding
                ? SettingsCategoryAdapters.BrandingKeys
                : null;

        var genericValues = tableManagedKeys is null
            ? request.Values
            : request.Values.Where(kv => !tableManagedKeys.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        await SaveGenericAsync(connection, tenantId, request.Category, genericValues, cancellationToken);

        if (isSecurity)
            await SettingsCategoryAdapters.SaveSecurityAsync(connection, tenantId, request.Values, cancellationToken);
        else if (isBranding)
            await SettingsCategoryAdapters.SaveBrandingAsync(connection, tenantId, request.Values, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Settings updated.");
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
}

/// <summary>
/// Maps the generic key/value settings contract onto the existing normalized tenant tables so
/// Security and Branding categories reuse their established storage instead of duplicating columns.
/// </summary>
internal static class SettingsCategoryAdapters
{
    public static readonly HashSet<string> SecurityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "IsMfaRequired", "PasswordExpiryDays", "SessionTimeoutMinutes",
        "IsGdprEnabled", "IsAuditLoggingEnabled", "IsVatEnabled"
    };

    public static readonly HashSet<string> BrandingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "LogoUrl", "PrimaryColor", "Website", "SupportEmail", "Country", "CurrencyCode", "TimeZone"
    };

    public static async Task OverlaySecurityAsync(
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

    public static async Task SaveSecurityAsync(
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

    public static async Task OverlayBrandingAsync(
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

    public static async Task SaveBrandingAsync(
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
