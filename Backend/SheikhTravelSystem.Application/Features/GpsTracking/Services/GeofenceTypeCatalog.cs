namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Built-in geofence types. The type is stored in <c>Geofences.Category</c> by name; matching ignores case so the
/// categories the old page saved (e.g. "Fuel Station", "Delivery Zone") keep their type. Tenants add their own in
/// <c>GeofenceTypes</c>. Red is reserved for restricted types.
/// </summary>
public static class GeofenceTypeCatalog
{
    public sealed record BuiltInType(string Name, string Color, bool IsRestricted);

    public const string RestrictedColor = "#dc2626";

    public static readonly IReadOnlyList<BuiltInType> BuiltIn =
    [
        new("Office", "#0f766e", false),
        new("Depot", "#0f766e", false),
        new("Warehouse", "#0f766e", false),
        new("Customer", "#2563eb", false),
        new("Delivery zone", "#2563eb", false),
        new("Fuel station", "#b45309", false),
        new("Service area", "#b45309", false),
        new("Parking", "#475569", false),
        new("Home", "#475569", false),
        new("Restricted", RestrictedColor, true),
        new("Route corridor", "#7c3aed", false),
    ];

    public static BuiltInType? Find(string? name)
    {
        var key = name?.Trim();
        return string.IsNullOrEmpty(key)
            ? null
            : BuiltIn.FirstOrDefault(t => string.Equals(t.Name, key, StringComparison.OrdinalIgnoreCase));
    }
}
