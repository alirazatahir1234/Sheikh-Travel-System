using System.Text.Json.Serialization;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

public record TraccarServer(
    string? Version,
    bool? Registration,
    bool? Readonly,
    bool? DeviceReadonly,
    int? PoiLayer,
    bool? ForceSettings,
    bool? MapOverride,
    int? CoordinateFormat);

public record TraccarDevice(
    int Id,
    string Name,
    string UniqueId,
    string Status,
    string? Category,
    string? Phone,
    string? Model,
    string? Contact,
    bool Disabled,
    [property: JsonPropertyName("lastUpdate")] DateTime? LastUpdate);

public record TraccarPosition(
    int Id,
    int DeviceId,
    string Protocol,
    [property: JsonPropertyName("serverTime")] DateTime ServerTime,
    [property: JsonPropertyName("deviceTime")] DateTime DeviceTime,
    [property: JsonPropertyName("fixTime")] DateTime FixTime,
    bool Outdated,
    bool Valid,
    double Latitude,
    double Longitude,
    double Altitude,
    double Speed,
    double Course,
    string? Address,
    double Accuracy,
    [property: JsonPropertyName("attributes")] TraccarPositionAttributes Attributes);

public record TraccarPositionAttributes(
    [property: JsonPropertyName("ignition")] bool? Ignition,
    [property: JsonPropertyName("fuel")] decimal? Fuel,
    [property: JsonPropertyName("batteryLevel")] decimal? BatteryLevel,
    [property: JsonPropertyName("rssi")] int? Rssi,
    [property: JsonPropertyName("distance")] decimal? Distance,
    [property: JsonPropertyName("totalDistance")] decimal? TotalDistance,
    [property: JsonPropertyName("motion")] bool? Motion,
    [property: JsonPropertyName("alarm")] string? Alarm = null,
    [property: JsonPropertyName("temperature")] decimal? Temperature = null,
    [property: JsonPropertyName("temp")] decimal? Temp = null,
    [property: JsonPropertyName("deviceTemp")] decimal? DeviceTemp = null,
    [property: JsonPropertyName("temp1")] decimal? Temp1 = null,
    [property: JsonPropertyName("temp2")] decimal? Temp2 = null)
{
    public decimal? ResolvedTemperature => Temperature ?? Temp ?? DeviceTemp ?? Temp1 ?? Temp2;
}

public record TraccarGeofence(
    int Id,
    string Name,
    string? Description,
    string Area,
    string? CalendarId);

public record TraccarTrip(
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("startTime")] DateTime StartTime,
    [property: JsonPropertyName("endTime")] DateTime EndTime,
    [property: JsonPropertyName("startLat")] double StartLat,
    [property: JsonPropertyName("startLon")] double StartLon,
    [property: JsonPropertyName("endLat")] double EndLat,
    [property: JsonPropertyName("endLon")] double EndLon,
    [property: JsonPropertyName("distance")] double Distance,
    [property: JsonPropertyName("averageSpeed")] double AverageSpeed,
    [property: JsonPropertyName("maxSpeed")] double MaxSpeed,
    [property: JsonPropertyName("duration")] int Duration,
    [property: JsonPropertyName("spentFuel")] decimal? SpentFuel = null,
    [property: JsonPropertyName("startAddress")] string? StartAddress = null,
    [property: JsonPropertyName("endAddress")] string? EndAddress = null,
    [property: JsonPropertyName("driverName")] string? DriverName = null,
    [property: JsonPropertyName("driverUniqueId")] string? DriverUniqueId = null,
    [property: JsonPropertyName("startOdometer")] double? StartOdometer = null,
    [property: JsonPropertyName("endOdometer")] double? EndOdometer = null);

public record TraccarStop(
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("startTime")] DateTime StartTime,
    [property: JsonPropertyName("endTime")] DateTime EndTime,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("duration")] int Duration,
    [property: JsonPropertyName("address")] string? Address);

public record TraccarEvent(
    int Id,
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("positionId")] int? PositionId,
    [property: JsonPropertyName("geofenceId")] int? GeofenceId,
    string Type,
    [property: JsonPropertyName("eventTime")] DateTime EventTime,
    double? Latitude = null,
    double? Longitude = null,
    string? Address = null,
    [property: JsonPropertyName("speed")] double? SpeedKnots = null,
    [property: JsonPropertyName("attributes")] TraccarEventAttributes? Attributes = null);

public record TraccarEventAttributes(
    [property: JsonPropertyName("alarm")] string? Alarm = null);

public record TraccarSummary(
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("deviceName")] string DeviceName,
    [property: JsonPropertyName("distance")] double Distance,
    [property: JsonPropertyName("averageSpeed")] double AverageSpeed,
    [property: JsonPropertyName("maxSpeed")] double MaxSpeed,
    [property: JsonPropertyName("engineHours")] long EngineHours,
    [property: JsonPropertyName("spentFuel")] decimal SpentFuel);

public record TraccarCommandType(
    [property: JsonPropertyName("type")] string Type);

public record TraccarStatusDto(
    bool Connected,
    string? ServerVersion,
    int DeviceCount,
    string? LastError = null,
    bool SyncEnabled = true);

public record TraccarSyncResultDto(int Imported, int Updated, int Skipped);
