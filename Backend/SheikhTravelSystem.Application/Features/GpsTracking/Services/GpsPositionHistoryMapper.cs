using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Services;

/// <summary>
/// Minimal GpsPositions row for Dapper. <see cref="PositionDto"/> has optional trailing
/// columns that break materialization when the SELECT only returns the core history fields.
/// </summary>
public sealed record GpsPositionHistoryRow(
    long Id,
    int VehicleId,
    int? DriverId,
    int? BookingId,
    int? GpsDeviceId,
    double Latitude,
    double Longitude,
    decimal Speed,
    double? Heading,
    double? Altitude,
    bool? Ignition,
    DateTime Timestamp,
    string? Address = null);

/// <summary>
/// Dapper row for live fleet positions. Mutable class so nullable SQL columns map correctly;
/// <see cref="PositionDto"/> positional records break materialization against reader types.
/// </summary>
public sealed class LivePositionRow
{
    public long Id { get; set; }
    public int VehicleId { get; set; }
    public int? DriverId { get; set; }
    public int? BookingId { get; set; }
    public int? GpsDeviceId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public decimal Speed { get; set; }
    public double? Heading { get; set; }
    public double? Altitude { get; set; }
    public bool? Ignition { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal? FuelLevel { get; set; }
    public decimal? BatteryLevel { get; set; }
    public int? GsmSignal { get; set; }
    public decimal? TotalDistanceKm { get; set; }
    public string? Address { get; set; }
    public string? AlarmType { get; set; }
    public string? DriverPhone { get; set; }
    public decimal? Temperature { get; set; }
}

public static class GpsPositionHistoryMapper
{
    private const decimal KnotsToKmh = 1.852m;
    public const int MaxPlaybackPoints = 1500;

    public static PositionDto ToPositionDto(GpsPositionHistoryRow row) =>
        new(
            row.Id,
            row.VehicleId,
            row.DriverId,
            row.BookingId,
            row.GpsDeviceId,
            row.Latitude,
            row.Longitude,
            row.Speed,
            row.Heading,
            row.Altitude,
            row.Ignition,
            GpsUtcDateTime.AsUtc(row.Timestamp),
            Address: row.Address);

    public static PositionDto ToPositionDto(LivePositionRow row) =>
        new(
            row.Id,
            row.VehicleId,
            row.DriverId,
            row.BookingId,
            row.GpsDeviceId,
            row.Latitude,
            row.Longitude,
            row.Speed,
            row.Heading,
            row.Altitude,
            row.Ignition,
            GpsUtcDateTime.AsUtc(row.Timestamp),
            FuelLevel: row.FuelLevel,
            BatteryLevel: row.BatteryLevel,
            GsmSignal: row.GsmSignal,
            TotalDistanceKm: row.TotalDistanceKm,
            Address: row.Address,
            AlarmType: row.AlarmType,
            DriverPhone: row.DriverPhone,
            Temperature: row.Temperature);

    public static PositionDto FromTraccar(
        Traccar.TraccarPosition position,
        int vehicleId,
        int? gpsDeviceId) =>
        new(
            position.Id,
            vehicleId,
            DriverId: null,
            BookingId: null,
            GpsDeviceId: gpsDeviceId,
            position.Latitude,
            position.Longitude,
            Math.Round((decimal)position.Speed * KnotsToKmh, 1),
            position.Course,
            position.Altitude,
            position.Attributes?.Ignition,
            GpsUtcDateTime.AsUtc(position.FixTime),
            FuelLevel: position.Attributes?.Fuel,
            BatteryLevel: position.Attributes?.BatteryLevel,
            GsmSignal: position.Attributes?.Rssi,
            TotalDistanceKm: position.Attributes?.TotalDistance is { } meters
                ? Math.Round(meters / 1000m, 3)
                : null,
            Address: position.Address,
            AlarmType: position.Attributes?.Alarm,
            DriverPhone: null,
            Temperature: position.Attributes?.ResolvedTemperature);

    /// <summary>
    /// Keep playback payloads renderable while preserving first/last odometer for accurate distance.
    /// </summary>
    public static List<PositionDto> DownsampleForPlayback(List<PositionDto> points, int maxPoints = MaxPlaybackPoints)
    {
        if (points.Count <= maxPoints)
            return points;

        var firstOd = points[0].TotalDistanceKm;
        var lastOd = points[^1].TotalDistanceKm;

        var step = (double)points.Count / maxPoints;
        var result = new List<PositionDto>(maxPoints + 1);
        for (var i = 0; i < maxPoints; i++)
        {
            var idx = Math.Min((int)Math.Floor(i * step), points.Count - 1);
            result.Add(points[idx]);
        }

        var last = points[^1];
        if (result[^1].Timestamp != last.Timestamp)
            result.Add(last);

        if (result.Count >= 2)
        {
            result[0] = result[0] with { TotalDistanceKm = firstOd ?? result[0].TotalDistanceKm };
            result[^1] = result[^1] with { TotalDistanceKm = lastOd ?? result[^1].TotalDistanceKm };
        }

        return result;
    }
}
