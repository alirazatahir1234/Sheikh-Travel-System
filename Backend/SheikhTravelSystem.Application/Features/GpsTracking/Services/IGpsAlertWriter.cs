using System.Data;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Single insert path for GpsAlertEvents. SQL lives in Infrastructure.
/// </summary>
public interface IGpsAlertWriter
{
    Task<int> InsertAsync(
        IDbConnection connection,
        int vehicleId,
        double latitude,
        double longitude,
        decimal speed,
        string eventType,
        string message,
        DateTime timestamp,
        int? ruleId = null,
        int? geofenceId = null,
        int? driverId = null,
        string? externalEventId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Pure severity mapping — kept in Application so analytics/migrations stay aligned without SQL.
/// </summary>
public static class GpsAlertSeverity
{
    /// <summary>Kept in sync with the backfill CASE in GpsAlertsPhase8Migration.</summary>
    public static string SeverityFor(string eventType) => eventType switch
    {
        "sos" or "power_cut" => "critical",
        "speed_exceeded" or "geofence_exit" or "vehicle_offline" or "device_offline" or "alarm" => "high",
        "ignition_on" or "low_battery" or "gps_lost" or "low_fuel" or "geofence_enter" => "medium",
        "ignition_off" or "online" or "device_online" or "vehicle_online" => "low",
        _ => "medium"
    };
}
