using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

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
    public const string NotificationRetention = "NotificationRetention";
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
        new(NotificationRetention, "Notification Retention", "policy", "Inbox archive and permanent-delete retention policy.", true),
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

public class GetSettingsCategoriesQueryHandler(IAppCache cache)
    : IRequestHandler<GetSettingsCategoriesQuery, ApiResponse<IReadOnlyList<SettingsCategoryDto>>>
{
    public Task<ApiResponse<IReadOnlyList<SettingsCategoryDto>>> Handle(
        GetSettingsCategoriesQuery request, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            "settings:categories",
            AppCacheTtl.SettingsCategories,
            static _ => Task.FromResult(
                ApiResponse<IReadOnlyList<SettingsCategoryDto>>.SuccessResponse(SettingsCategories.All)),
            cancellationToken);
}

public class GetSettingsByCategoryQueryHandler(
    ISettingsRepository settingsRepository,
    ITenantContext tenantContext,
    IAppCache cache)
    : IRequestHandler<GetSettingsByCategoryQuery, ApiResponse<IReadOnlyDictionary<string, string?>>>
{
    public async Task<ApiResponse<IReadOnlyDictionary<string, string?>>> Handle(
        GetSettingsByCategoryQuery request, CancellationToken cancellationToken)
    {
        if (!SettingsCategories.IsImplemented(request.Category))
            return ApiResponse<IReadOnlyDictionary<string, string?>>.FailResponse(
                $"Settings category '{request.Category}' is not available yet.");

        var tenantId = tenantContext.GetRequiredTenantId();
        var cacheKey = $"settings:{tenantId}:{request.Category}".ToLowerInvariant();

        return await cache.GetOrCreateAsync(
            cacheKey,
            AppCacheTtl.Settings,
            ct => LoadAsync(tenantId, request.Category, ct),
            cancellationToken);
    }

    private async Task<ApiResponse<IReadOnlyDictionary<string, string?>>> LoadAsync(
        int tenantId,
        string category,
        CancellationToken cancellationToken)
    {
        var values = await settingsRepository.GetByCategoryAsync(tenantId, category, cancellationToken);
        return ApiResponse<IReadOnlyDictionary<string, string?>>.SuccessResponse(values);
    }
}

public class UpdateSettingsCommandHandler(
    ISettingsRepository settingsRepository,
    ITenantContext tenantContext,
    IAppCache cache)
    : IRequestHandler<UpdateSettingsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!SettingsCategories.IsImplemented(request.Category))
            return ApiResponse<bool>.FailResponse(
                $"Settings category '{request.Category}' is not available yet.");

        var tenantId = tenantContext.GetRequiredTenantId();
        await settingsRepository.SaveByCategoryAsync(tenantId, request.Category, request.Values, cancellationToken);

        cache.Remove($"settings:{tenantId}:{request.Category}".ToLowerInvariant());

        return ApiResponse<bool>.SuccessResponse(true, "Settings updated.");
    }
}
