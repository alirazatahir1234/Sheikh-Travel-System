namespace SheikhTravelSystem.Application.Common;

/// <summary>Stage 10 Workspace Registry seed matching role workspace hints.</summary>
public static class WorkspaceRegistrySeed
{
    public sealed record WorkspaceSeed(
        string WorkspaceKey,
        string DisplayName,
        string? Description,
        string Category,
        string Icon,
        string HomeRoute,
        int SortOrder,
        bool Visible,
        bool IsMobileSupported,
        string? ModuleKeysJson,
        string? FeatureKey,
        string? DefaultDashboardKey);

    public static IReadOnlyList<WorkspaceSeed> All { get; } =
    [
        new("platform", "Platform", "Platform administration workspace", "Platform", "settings_applications",
            "/platform", 10, true, false,
            """["platform","organization","access_control","administration"]""", null, null),
        new("company", "Company", "Company operations overview", "Company", "business",
            "/dashboard", 20, true, false,
            """["dashboard","organization","administration","access_control","analytics"]""", null, null),
        new("fleet", "Fleet", "Fleet operations workspace", "Fleet", "local_shipping",
            "/dashboard", 30, true, true,
            """["fleet","dashboard"]""", null, null),
        new("drivers", "Drivers", "Driver management workspace", "Fleet", "badge",
            "/drivers", 40, true, true,
            """["fleet","dashboard"]""", null, null),
        new("trips", "Trips & Dispatch", "Bookings and trip operations", "Operations", "map",
            "/bookings", 50, true, true,
            """["operations","fleet","dashboard","customers"]""", null, null),
        new("finance", "Finance", "Payments and reporting workspace", "Finance", "payments",
            "/payments", 60, true, false,
            """["finance","analytics","dashboard"]""", null, null),
        new("driver", "Driver", "Field driver workspace", "Field", "directions_car",
            "/my-trips", 70, true, true,
            """["operations"]""", null, null),
        new("home", "Home", "Default landing workspace", "General", "home",
            "/dashboard", 100, true, true,
            null, null, null),
    ];
}
