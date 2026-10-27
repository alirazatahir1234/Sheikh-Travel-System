using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class FuelStationStopEvaluator(
    IGooglePlacesNearbyService placesNearby,
    IGpsAlertWriter alertWriter,
    INotificationDecisionEngine decisionEngine,
    IOptions<GpsSettings> gpsSettings,
    ILogger<FuelStationStopEvaluator> logger) : IFuelStationStopEvaluator
{
    public const string EventType = "fuel_station_stop";

    private sealed record StopState(DateTime StopStartedAt, double Lat, double Lon);

    private static readonly ConcurrentDictionary<int, StopState> ActiveStops = new();

    public async Task EvaluateAsync(
        IDbConnection connection,
        IngestPositionDto dto,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        var opts = gpsSettings.Value.FuelStationStop;
        if (!opts.Enabled)
            return;

        if (dto.Speed > opts.StopSpeedKmh)
        {
            ActiveStops.TryRemove(dto.VehicleId, out _);
            return;
        }

        var state = ActiveStops.AddOrUpdate(
            dto.VehicleId,
            _ => new StopState(timestamp, dto.Latitude, dto.Longitude),
            (_, existing) =>
            {
                var movedM = DistanceMeters(existing.Lat, existing.Lon, dto.Latitude, dto.Longitude);
                return movedM > 80
                    ? new StopState(timestamp, dto.Latitude, dto.Longitude)
                    : existing with { Lat = dto.Latitude, Lon = dto.Longitude };
            });

        var stopMinutes = (timestamp - state.StopStartedAt).TotalMinutes;
        if (stopMinutes < opts.MinStopMinutes)
            return;

        var recentAlert = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS(
                SELECT 1 FROM GpsAlertEvents
                WHERE VehicleId = @VehicleId AND EventType = @EventType AND IsDeleted = 0
                  AND Timestamp > DATEADD(MINUTE, -@Minutes, @Timestamp)
            ) THEN 1 ELSE 0 END
            """,
            new { dto.VehicleId, EventType, Minutes = opts.DedupMinutes, Timestamp = timestamp },
            cancellationToken: cancellationToken));

        if (recentAlert)
            return;

        var search = await placesNearby.SearchNearbyAsync(
            dto.Latitude,
            dto.Longitude,
            "fuel",
            opts.PlacesSearchRadiusMeters,
            maxResults: 5,
            cancellationToken);

        if (search.Places is null || search.Places.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(search.ErrorMessage))
                logger.LogDebug("Fuel station POI search skipped for vehicle {VehicleId}: {Reason}",
                    dto.VehicleId, search.ErrorMessage);
            return;
        }

        var nearest = search.Places
            .Select(p => new
            {
                Place = p,
                DistanceM = p.DistanceMeters
                           ?? DistanceMeters(dto.Latitude, dto.Longitude, p.Latitude, p.Longitude)
            })
            .OrderBy(x => x.DistanceM)
            .FirstOrDefault();

        if (nearest is null || nearest.DistanceM > opts.ProximityRadiusMeters)
            return;

        var stationLabel = string.IsNullOrWhiteSpace(nearest.Place.Name)
            ? "Petrol station"
            : nearest.Place.Name.Trim();

        var message =
            $"Possible fuel stop — {stationLabel} (~{nearest.DistanceM:F0} m). Verify fill-up in Fuel logs.";

        var alertId = await alertWriter.InsertAsync(
            connection,
            dto.VehicleId,
            dto.Latitude,
            dto.Longitude,
            dto.Speed,
            EventType,
            message,
            timestamp,
            driverId: dto.DriverId,
            cancellationToken: cancellationToken);

        if (alertId <= 0)
            return;

        ActiveStops.TryRemove(dto.VehicleId, out _);

        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
            new { Id = dto.VehicleId },
            cancellationToken: cancellationToken));

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            EventType,
            "Fuel station stop",
            message,
            NotificationType.EngineCommandSent,
            ReferenceId: dto.VehicleId,
            AlertEventId: alertId,
            TenantId: tenantId,
            SuggestedPriority: 2,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push
            ]), cancellationToken);
    }

    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        static double Rad(double d) => d * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
