namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// One geofence alert to deliver. <paramref name="DefaultChannels"/> apply when the geofence chose none.
/// <paramref name="ReferenceId"/> overrides the vehicle as the notification reference (e.g. the geofence for
/// "no vehicle arrived by", which has no vehicle).
/// </summary>
public sealed record GeofenceAlertNotice(
    int TenantId,
    int GeofenceId,
    int VehicleId,
    int? DriverId,
    int? AlertEventId,
    string EventType,
    string Title,
    string Message,
    int Priority,
    IReadOnlyList<string> DefaultChannels,
    int? ReferenceId = null);

/// <summary>
/// Delivers geofence alerts on the geofence's chosen channels to its chosen roles and users (the alert's default
/// recipients when none are chosen), plus a push to the vehicle's driver when "push to driver app" is on.
/// </summary>
public interface IGeofenceAlertNotifier
{
    Task NotifyAsync(GeofenceAlertNotice notice, CancellationToken cancellationToken = default);
}
