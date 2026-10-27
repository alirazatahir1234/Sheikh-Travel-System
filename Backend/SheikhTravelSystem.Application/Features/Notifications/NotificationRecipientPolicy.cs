using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Notifications;

public static class NotificationRecipientPolicy
{
    public static IReadOnlyList<UserRole> RolesFor(string eventType) => eventType.ToLowerInvariant() switch
    {
        "booking_created" => [UserRole.Admin, UserRole.Dispatcher],
        // Assigned driver is resolved contextually from Trips.DriverId → Drivers.UserId
        // (do not fan-out to every Driver role — that empties ops value and spams the fleet).
        "trip_driver_assigned" => [UserRole.Admin, UserRole.Dispatcher],
        "trip_created" or "trip_started" or "trip_completed" or "trip_delayed" or "trip_cancelled"
            or "trip_updated" or "trip_driver_arriving"
            => [UserRole.Admin, UserRole.Dispatcher],
        "payment_received" => [UserRole.Admin],
        "fuel_added" or "inspection_submitted" => [UserRole.Admin, UserRole.Dispatcher, UserRole.Driver],
        "compliance_reminder" or "vehicle_offline" or "speed_exceeded" or "road_speed_exceeded" or "sos"
            or "ignition_on" or "ignition_off"
            or "driver_voice_alert" or "driver_voice_message"
            => [UserRole.Admin, UserRole.Dispatcher],
        "sos_team_dispatch" => [UserRole.Driver],
        "system_announcement" => [UserRole.Admin, UserRole.Dispatcher, UserRole.Driver, UserRole.Accountant],
        _ => [UserRole.Admin, UserRole.Dispatcher]
    };

    public static bool AllowsBroadcast(string eventType) =>
        string.Equals(eventType, "system_announcement", StringComparison.OrdinalIgnoreCase);
}
