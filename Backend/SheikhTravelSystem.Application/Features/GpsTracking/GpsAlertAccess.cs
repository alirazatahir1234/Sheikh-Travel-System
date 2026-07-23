using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.GpsTracking;

internal static class GpsAlertAccess
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> RoleEventTypes =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DRIVER"] =
            [
                "trip_assigned", "trip_started", "trip_completed", "trip_cancelled",
                "driver_assigned", "speed_exceeded", "overspeed", "harsh_braking",
                "seatbelt", "rest_time", "license_expiry", "document_expiry", "inspection_required"
            ],
            ["DISPATCHER"] =
            [
                "trip_started", "trip_completed", "trip_cancelled", "driver_assigned",
                "driver_late", "route_deviation", "geofence_exit", "speed_exceeded",
                "overspeed", "idle_vehicle", "gps_offline", "vehicle_offline", "booking_cancellation",
                "eta_delay", "vehicle_arrived", "vehicle_departed"
            ],
            ["FLEET_MANAGER"] =
            [
                "speed_exceeded", "overspeed", "harsh_braking", "harsh_acceleration",
                "engine_fault", "fuel_theft", "low_battery", "gps_offline", "vehicle_offline",
                "maintenance_due", "geofence_enter", "geofence_exit", "idle_vehicle",
                "ignition_on", "ignition_off", "driver_assigned", "inspection_failed"
            ],
            ["DRIVER_MANAGER"] =
            [
                "driver_late", "driver_absent", "training_due", "license_expiry",
                "document_expiry", "inspection_required", "trip_assigned"
            ],
            ["TENANT_ADMIN"] = [],
            ["SUPER_ADMIN"] = []
        };

    public static bool CanView(ICurrentUserService currentUser) =>
        currentUser.HasPermission(GpsPermissions.AlertView)
        || currentUser.HasPermission("GPS.View");

    public static bool CanAcknowledge(ICurrentUserService currentUser) =>
        currentUser.HasPermission(GpsPermissions.AlertAcknowledge);

    public static bool CanResolve(ICurrentUserService currentUser) =>
        currentUser.HasPermission(GpsPermissions.AlertResolve);

    public static bool CanArchive(ICurrentUserService currentUser) =>
        currentUser.HasPermission(GpsPermissions.AlertArchive);

    public static bool CanDelete(ICurrentUserService currentUser) =>
        currentUser.HasPermission(GpsPermissions.AlertDelete);

    public static string? NormalizeEventType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Trim().ToLowerInvariant() switch
        {
            "vehicle_offline" => "gps_offline",
            "device_offline" => "gps_offline",
            "vehicle_online" => "gps_online",
            "speed_exceeded" => "overspeed",
            _ => raw.Trim().ToLowerInvariant()
        };
    }

    public static IReadOnlyCollection<string>? AllowedEventTypes(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        return RoleEventTypes.TryGetValue(role.Trim(), out var set) && set.Count > 0
            ? set
            : null;
    }
}
