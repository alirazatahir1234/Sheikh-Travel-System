using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Stage 7 Role Registry seed metadata (system roles) for migration + API enrichment.
/// </summary>
public static class RoleRegistrySeed
{
    public sealed record Entry(
        string Code,
        string Name,
        string DisplayName,
        string Description,
        string Category,
        int SortOrder,
        string RoleType);

    public static IReadOnlyList<Entry> All { get; } =
    [
        E("SUPER_ADMIN", "Super Admin", "Super Admin", "Platform-wide administration", "Platform", 5, "System"),
        E("TENANT_ADMIN", "Tenant Admin", "Company Admin", "Full company administration", "Platform", 10, "System"),
        E("FLEET_MANAGER", "Fleet Manager", "Fleet Manager", "Fleet, vehicles, drivers, GPS", "Fleet", 20, "System"),
        E("DRIVER_MANAGER", "Driver Manager", "Driver Manager", "Driver directory and assignments", "Fleet", 25, "System"),
        E("DISPATCHER", "Dispatcher", "Dispatcher", "Bookings, trips, and dispatch", "Operations", 30, "System"),
        E("ACCOUNTANT", "Accountant", "Accountant", "Payments, invoices, and reports", "Finance", 40, "System"),
        E("DRIVER", "Driver", "Driver", "Field driver trip and GPS access", "Fleet", 50, "System"),
    ];

    private static Entry E(
        string code, string name, string displayName, string description, string category, int sort, string roleType)
        => new(code, name, displayName, description, category, sort, roleType);

    public static Entry? Find(string code)
        => All.FirstOrDefault(e => string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Maps legacy Users.Role enum to platform role code.</summary>
    public static string MapLegacyRoleCode(UserRole role) => role switch
    {
        UserRole.Admin => "TENANT_ADMIN",
        UserRole.Dispatcher => "DISPATCHER",
        UserRole.Driver => "DRIVER",
        UserRole.Accountant => "ACCOUNTANT",
        _ => "TENANT_ADMIN"
    };
}
