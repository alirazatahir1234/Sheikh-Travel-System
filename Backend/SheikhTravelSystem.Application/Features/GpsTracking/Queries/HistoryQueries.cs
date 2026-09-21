using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetHistoryReplayQuery(
    int? VehicleId,
    int? DeviceId,
    DateTime? FromDate,
    DateTime? ToDate,
    int? RouteMaxPoints = null,
    int? PlaybackMaxPoints = null,
    bool IncludeRaw = false)
    : IRequest<ApiResponse<HistoryReplayBundleDto>>;

public class GetHistoryReplayQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetHistoryReplayQuery, ApiResponse<HistoryReplayBundleDto>>
{
    public Task<ApiResponse<HistoryReplayBundleDto>> Handle(GetHistoryReplayQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetHistoryReplayAsync(request, cancellationToken);
}

public record PostHistoryReplayInsightsCommand(
    int VehicleId,
    DateTime? FromDate,
    DateTime? ToDate)
    : IRequest<ApiResponse<GpsOperatorInsightDto>>;

public class PostHistoryReplayInsightsHandler(IMediator mediator)
    : IRequestHandler<PostHistoryReplayInsightsCommand, ApiResponse<GpsOperatorInsightDto>>
{
    public async Task<ApiResponse<GpsOperatorInsightDto>> Handle(
        PostHistoryReplayInsightsCommand request,
        CancellationToken cancellationToken)
    {
        var replay = await mediator.Send(
            new GetHistoryReplayQuery(request.VehicleId, null, request.FromDate, request.ToDate),
            cancellationToken);
        if (!replay.Success || replay.Data is null)
        {
            return ApiResponse<GpsOperatorInsightDto>.FailResponse(
                replay.Message ?? "Unable to load replay for this range.");
        }

        var data = replay.Data;
        var stats = data.Statistics;
        var summary = data.Summary;
        var dist = data.MileageKm ?? stats?.DistanceKm ?? summary?.DistanceKm ?? 0;
        var drive = stats?.DrivingMinutes ?? summary?.DrivingMinutes ?? 0;
        var idle = stats?.IdleMinutes ?? 0;
        var max = stats?.MaxSpeedKmh ?? summary?.MaxSpeedKmh ?? 0;
        var overs = stats?.OverspeedCount ?? 0;
        var stops = data.Stops.Count;
        var geofence = data.Events.Count(e =>
            e.Type.Contains("geofence", StringComparison.OrdinalIgnoreCase));

        var bullets = new List<string>
        {
            $"Distance: {dist:0.1} km",
            $"Driving: {drive / 60}h {drive % 60}m",
            $"Idle: {idle} min",
            $"Max speed: {max:0} km/h",
            $"Stops: {stops}",
            $"Overspeed events: {overs}",
            $"Geofence events: {geofence}",
        };

        var narrative =
            $"Vehicle traveled {dist:0.1} km in {drive / 60}h {drive % 60}m"
            + (overs > 0 ? $", exceeded speed threshold {overs} time(s)" : "")
            + (idle > 0 ? $", idled {idle} minutes" : "")
            + (stops > 0 ? $", {stops} stop(s)" : "")
            + (geofence > 0 ? $", {geofence} geofence event(s)" : "")
            + ".";

        return ApiResponse<GpsOperatorInsightDto>.SuccessResponse(
            new GpsOperatorInsightDto("Trip replay insight", narrative, bullets));
    }
}

public record HistoryExportFileDto(byte[] Bytes, string ContentType, string FileName);

public record GetHistoryExportQuery(int VehicleId, DateTime? FromDate, DateTime? ToDate, string Format)
    : IRequest<ApiResponse<HistoryExportFileDto>>;

public class GetHistoryExportQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetHistoryExportQuery, ApiResponse<HistoryExportFileDto>>
{
    public Task<ApiResponse<HistoryExportFileDto>> Handle(GetHistoryExportQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetHistoryExportAsync(request, cancellationToken);
}

internal static class HistoryExportFormatter
{
    public static string ToCsv(IReadOnlyList<PositionDto> positions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,latitude,longitude,speed_kmh,heading,ignition,address,odometer_km");
        foreach (var p in positions)
        {
            var ignition = p.Ignition switch
            {
                true => "1",
                false => "0",
                _ => ""
            };
            sb.Append(p.Timestamp.ToString("o", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(p.Latitude.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(p.Longitude.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(p.Speed.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(p.Heading?.ToString(CultureInfo.InvariantCulture) ?? "");
            sb.Append(',');
            sb.Append(ignition);
            sb.Append(',');
            sb.Append(EscapeCsv(p.Address));
            sb.Append(',');
            sb.Append(p.TotalDistanceKm?.ToString(CultureInfo.InvariantCulture) ?? "");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string ToGpx(IReadOnlyList<PositionDto> positions, int vehicleId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<gpx version=\"1.1\" creator=\"SheikhGo\" xmlns=\"http://www.topografix.com/GPX/1/1\">");
        sb.AppendLine($"  <trk><name>Vehicle {vehicleId}</name><trkseg>");
        foreach (var p in positions)
        {
            sb.Append("    <trkpt lat=\"");
            sb.Append(p.Latitude.ToString(CultureInfo.InvariantCulture));
            sb.Append("\" lon=\"");
            sb.Append(p.Longitude.ToString(CultureInfo.InvariantCulture));
            sb.Append("\">");
            sb.Append("<time>");
            sb.Append(p.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            sb.Append("</time>");
            if (p.Speed > 0)
            {
                sb.Append("<extensions><speed>");
                sb.Append(p.Speed.ToString(CultureInfo.InvariantCulture));
                sb.Append("</speed></extensions>");
            }
            sb.AppendLine("</trkpt>");
        }
        sb.AppendLine("  </trkseg></trk></gpx>");
        return sb.ToString();
    }

    public static string ToKml(IReadOnlyList<PositionDto> positions, int vehicleId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
        sb.AppendLine("  <Document>");
        sb.Append("    <name>Vehicle ");
        sb.Append(vehicleId);
        sb.AppendLine("</name>");
        sb.AppendLine("    <Placemark>");
        sb.Append("      <name>Route ");
        sb.Append(vehicleId);
        sb.AppendLine("</name>");
        sb.AppendLine("      <Style><LineStyle><color>ffed4d1d</color><width>4</width></LineStyle></Style>");
        sb.AppendLine("      <LineString><tessellate>1</tessellate><coordinates>");
        foreach (var p in positions)
        {
            sb.Append("        ");
            sb.Append(p.Longitude.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(p.Latitude.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(",0");
        }
        sb.AppendLine("      </coordinates></LineString>");
        sb.AppendLine("    </Placemark>");
        sb.AppendLine("  </Document>");
        sb.AppendLine("</kml>");
        return sb.ToString();
    }

    public static string ToGeoJson(IReadOnlyList<PositionDto> positions)
    {
        var features = positions.Select(p => new
        {
            type = "Feature",
            geometry = new
            {
                type = "Point",
                coordinates = new[] { p.Longitude, p.Latitude }
            },
            properties = new
            {
                timestamp = p.Timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                speedKmh = p.Speed,
                heading = p.Heading,
                ignition = p.Ignition,
                address = p.Address,
                odometerKm = p.TotalDistanceKm
            }
        });

        var root = new
        {
            type = "FeatureCollection",
            features
        };

        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
            return value;
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
