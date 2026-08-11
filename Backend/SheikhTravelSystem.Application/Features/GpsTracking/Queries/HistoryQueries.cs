using Dapper;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetHistoryReplayQuery(int? VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<HistoryReplayBundleDto>>;

public class GetHistoryReplayQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext,
    IReverseGeocodingService reverseGeocodingService)
    : IRequestHandler<GetHistoryReplayQuery, ApiResponse<HistoryReplayBundleDto>>
{
    public async Task<ApiResponse<HistoryReplayBundleDto>> Handle(
        GetHistoryReplayQuery request,
        CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        fromDate = GpsUtcDateTime.AsUtc(fromDate);
        toDate = GpsUtcDateTime.AsUtc(toDate);

        var validation = TripVehicleQueryHelper.ValidateHistoryRequest<HistoryReplayBundleDto>(
            request.VehicleId, fromDate, toDate);
        if (validation is not null) return validation;

        var vehicleId = request.VehicleId!.Value;
        var source = await TripVehicleQueryHelper.ResolveVehicleAsync(
            dbFactory, tenantContext, vehicleId, cancellationToken);
        if (source is null)
            return ApiResponse<HistoryReplayBundleDto>.FailResponse("Vehicle not found.");

        var vehicleContext = await TripVehicleQueryHelper.BuildContextAsync(
            dbFactory, tenantContext, vehicleId, cancellationToken);

        IReadOnlyDictionary<int, string>? geofenceNames = null;
        var opts = traccarOptions.Value;
        TripReplayBundleDto bundle;

        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            geofenceNames = await LoadGeofenceNamesAsync(traccarClient, cancellationToken);
            bundle = await TripReplayLoader.LoadFromTraccarAsync(
                traccarClient,
                source.TraccarDeviceId.Value,
                fromDate,
                toDate,
                cancellationToken,
                geofenceNames,
                routeMaxPoints: 5000,
                playbackMaxPoints: 2500);
        }
        else
        {
            bundle = await TripReplayLoader.LoadFromLocalHistoryAsync(
                mediator, vehicleId, fromDate, toDate, cancellationToken);
        }

        if (bundle.Route.Count == 0 && bundle.Playback.Count == 0)
            return ApiResponse<HistoryReplayBundleDto>.FailResponse("No tracking points in this period.");

        bundle = await TripReplayAddressEnricher.EnrichAsync(
            bundle,
            reverseGeocodingService,
            cancellationToken);

        TripAnalyticsSummaryDto? rawStats = null;
        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            var deviceId = source.TraccarDeviceId.Value;
            var summaryTask = traccarClient.GetSummaryAsync(deviceId, fromDate, toDate, cancellationToken);
            var tripsTask = mediator.Send(
                new GetGpsTripsQuery(vehicleId, fromDate, toDate, Unpaged: true),
                cancellationToken);
            await Task.WhenAll(summaryTask, tripsTask);

            var tripsResponse = await tripsTask;
            var trips = tripsResponse.Success && tripsResponse.Data is not null
                ? tripsResponse.Data.Items
                : [];

            // Stops/events already loaded in the replay bundle — avoid a second Traccar round-trip.
            var traccarStops = Array.Empty<TraccarStop>();
            var traccarEvents = Array.Empty<TraccarEvent>();
            rawStats = TripAnalyticsMapper.BuildSummary(
                trips,
                (await summaryTask).ToArray(),
                traccarStops,
                traccarEvents);
        }

        var mileageKm = TripAnalyticsMapper.ComputeOdometerMileageKm(bundle.Route)
            ?? bundle.Summary?.DistanceKm;

        var statistics = TripAnalyticsMapper.BuildHistoryStatistics(
            rawStats,
            bundle.Route,
            bundle.Stops,
            fromDate,
            toDate,
            mileageKm);

        // Keep replay summary driving time consistent with clamped history stats.
        var summary = bundle.Summary is null
            ? null
            : bundle.Summary with
            {
                DistanceKm = mileageKm ?? bundle.Summary.DistanceKm,
                DrivingMinutes = statistics.DrivingMinutes,
                AvgSpeedKmh = statistics.AvgSpeedKmh,
                MaxSpeedKmh = statistics.MaxSpeedKmh,
                EngineHours = statistics.EngineHours
            };

        return ApiResponse<HistoryReplayBundleDto>.SuccessResponse(
            new HistoryReplayBundleDto(
                bundle.Route,
                bundle.Playback,
                bundle.Stops,
                bundle.Events,
                summary,
                statistics,
                mileageKm,
                vehicleContext));
    }

    private static async Task<IReadOnlyDictionary<int, string>> LoadGeofenceNamesAsync(
        ITraccarClient traccarClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var geofences = await traccarClient.GetGeofencesAsync(cancellationToken);
            return geofences.ToDictionary(g => g.Id, g => g.Name);
        }
        catch
        {
            return new Dictionary<int, string>();
        }
    }
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
            new GetHistoryReplayQuery(request.VehicleId, request.FromDate, request.ToDate),
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

public class GetHistoryExportQueryHandler(
    IDbConnectionFactory dbFactory,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions)
    : IRequestHandler<GetHistoryExportQuery, ApiResponse<HistoryExportFileDto>>
{
    private const int MaxExportRows = 50_000;
    private const int SparseRemoteThreshold = 5;

    public async Task<ApiResponse<HistoryExportFileDto>> Handle(
        GetHistoryExportQuery request,
        CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;
        fromDate = GpsUtcDateTime.AsUtc(fromDate);
        toDate = GpsUtcDateTime.AsUtc(toDate);

        if (fromDate > toDate)
            return ApiResponse<HistoryExportFileDto>.FailResponse("'from' must be before 'to'.");
        if (toDate - fromDate > TripVehicleQueryHelper.MaxHistoryRange)
            return ApiResponse<HistoryExportFileDto>.FailResponse("Date range cannot exceed 366 days.");

        var format = (request.Format ?? "csv").Trim().ToLowerInvariant();
        if (format is not ("csv" or "gpx" or "geojson" or "kml"))
            return ApiResponse<HistoryExportFileDto>.FailResponse("Format must be csv, gpx, geojson, or kml.");

        var positions = await LoadFullPositionsAsync(
            dbFactory, traccarClient, traccarOptions.Value, request.VehicleId, fromDate, toDate, cancellationToken);

        if (positions.Count == 0)
            return ApiResponse<HistoryExportFileDto>.FailResponse("No tracking points in this period.");

        if (positions.Count > MaxExportRows)
            positions = positions.Take(MaxExportRows).ToList();

        var stamp = fromDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var stampTo = toDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var baseName = $"gps-history-{request.VehicleId}-{stamp}-{stampTo}";

        return format switch
        {
            "gpx" => ApiResponse<HistoryExportFileDto>.SuccessResponse(
                new HistoryExportFileDto(
                    Encoding.UTF8.GetBytes(HistoryExportFormatter.ToGpx(positions, request.VehicleId)),
                    "application/gpx+xml",
                    $"{baseName}.gpx")),
            "kml" => ApiResponse<HistoryExportFileDto>.SuccessResponse(
                new HistoryExportFileDto(
                    Encoding.UTF8.GetBytes(HistoryExportFormatter.ToKml(positions, request.VehicleId)),
                    "application/vnd.google-earth.kml+xml",
                    $"{baseName}.kml")),
            "geojson" => ApiResponse<HistoryExportFileDto>.SuccessResponse(
                new HistoryExportFileDto(
                    Encoding.UTF8.GetBytes(HistoryExportFormatter.ToGeoJson(positions)),
                    "application/geo+json",
                    $"{baseName}.geojson")),
            _ => ApiResponse<HistoryExportFileDto>.SuccessResponse(
                new HistoryExportFileDto(
                    Encoding.UTF8.GetBytes(HistoryExportFormatter.ToCsv(positions)),
                    "text/csv",
                    $"{baseName}.csv"))
        };
    }

    private static async Task<List<PositionDto>> LoadFullPositionsAsync(
        IDbConnectionFactory dbFactory,
        ITraccarClient traccarClient,
        TraccarOptions opts,
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        if (opts.Enabled)
        {
            var link = await connection.QuerySingleOrDefaultAsync<(int? GpsDeviceId, int? TraccarDeviceId)>(
                new CommandDefinition(
                    """
                    SELECT TOP 1 d.Id AS GpsDeviceId, d.TraccarDeviceId
                    FROM GpsDevices d
                    WHERE d.VehicleId = @VehicleId AND d.IsDeleted = 0 AND d.TraccarDeviceId IS NOT NULL
                    ORDER BY d.Id DESC
                    """,
                    new { VehicleId = vehicleId },
                    cancellationToken: cancellationToken));

            if (link.TraccarDeviceId is int traccarDeviceId)
            {
                var route = await traccarClient.GetRouteAsync(traccarDeviceId, fromDate, toDate, cancellationToken);
                if (route.Count < SparseRemoteThreshold)
                {
                    var remotePositions = await traccarClient.GetPositionsByDeviceAsync(
                        traccarDeviceId, fromDate, toDate, cancellationToken);
                    if (remotePositions.Count > route.Count)
                        route = remotePositions;
                }

                if (route.Count > 0)
                {
                    return route
                        .Select(p => GpsPositionHistoryMapper.FromTraccar(p, vehicleId, link.GpsDeviceId))
                        .OrderBy(p => p.Timestamp)
                        .ToList();
                }
            }
        }

        var localRows = await connection.QueryAsync<GpsPositionHistoryRow>(new CommandDefinition(
            @"SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                     Heading, Altitude, Ignition, RecordedAt AS Timestamp, Address
              FROM GpsPositions
              WHERE VehicleId = @VehicleId AND RecordedAt BETWEEN @FromDate AND @ToDate
              ORDER BY RecordedAt ASC",
            new { VehicleId = vehicleId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        return localRows.Select(GpsPositionHistoryMapper.ToPositionDto).ToList();
    }
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
