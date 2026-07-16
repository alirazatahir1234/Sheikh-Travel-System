using Dapper;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetDriverScoreRankingQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null)
    : IRequest<ApiResponse<List<DriverScoreDto>>>;

/// <summary>
/// Attributes GpsTrips to drivers via an AssignmentHistory time-window overlap join (GpsTrips has no
/// DriverId column). Overspeed comes directly from GpsAlertEvents.DriverId (already resolved at
/// write time by GpsAlertWriter — no join needed there). Idle/harsh-event factors reuse the same
/// uncapped per-device Traccar fetch pattern as the Day 3 Idle/Stop analytics, attributed to a
/// driver via the same vehicle+timestamp window lookup used for trips.
/// </summary>
public class GetDriverScoreRankingQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverScoreRankingQuery, ApiResponse<List<DriverScoreDto>>>
{
    private const int NightStartHour = 22;
    private const int NightEndHour = 5;

    public async Task<ApiResponse<List<DriverScoreDto>>> Handle(GetDriverScoreRankingQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var tripsResponse = await mediator.Send(
            new GetGpsTripsQuery(null, fromDate, toDate, request.BranchId, request.DepartmentId, null, Unpaged: true),
            cancellationToken);

        if (!tripsResponse.Success || tripsResponse.Data is null)
            return ApiResponse<List<DriverScoreDto>>.FailResponse(tripsResponse.Message ?? "Failed to load trips.");

        var trips = tripsResponse.Data.Items;
        if (trips.Count == 0)
            return ApiResponse<List<DriverScoreDto>>.SuccessResponse([]);

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var vehicleIds = trips.Select(t => t.VehicleId).Distinct().ToList();

        // AssignmentHistory windows overlapping the range for these vehicles — the join a trip (or
        // a Traccar stop/event, keyed by the same VehicleId+timestamp) is attributed to a driver through.
        var assignments = (await connection.QueryAsync<(int VehicleId, int? DriverId, DateTime StartAt, DateTime? EndAt)>(
            new CommandDefinition(
                """
                SELECT VehicleId, DriverId, StartAt, EndAt
                FROM AssignmentHistory
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND DriverId IS NOT NULL
                  AND VehicleId IN @VehicleIds
                  AND StartAt <= @ToDate AND (EndAt IS NULL OR EndAt >= @FromDate)
                """,
                new { TenantId = tenantId, VehicleIds = vehicleIds, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

        int? ResolveDriver(int vehicleId, DateTime at) =>
            assignments.FirstOrDefault(a => a.VehicleId == vehicleId && a.StartAt <= at && (a.EndAt == null || a.EndAt >= at)).DriverId;

        var attributedTrips = trips
            .Select(t => (Trip: t, DriverId: ResolveDriver(t.VehicleId, t.StartTime)))
            .Where(x => x.DriverId.HasValue)
            .ToList();

        if (attributedTrips.Count == 0)
            return ApiResponse<List<DriverScoreDto>>.SuccessResponse([]);

        var driverNames = (await connection.QueryAsync<(int Id, string FullName)>(new CommandDefinition(
            "SELECT Id, FullName FROM Drivers WHERE TenantId = @TenantId AND IsDeleted = 0",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken))).ToDictionary(d => d.Id, d => d.FullName);

        var overspeedByDriver = (await connection.QueryAsync<(int DriverId, int Count)>(new CommandDefinition(
            """
            SELECT e.DriverId, COUNT(*) AS Count
            FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.EventType IN ('overspeed', 'speed_exceeded') AND e.IsDeleted = 0
              AND e.DriverId IS NOT NULL AND e.Timestamp BETWEEN @FromDate AND @ToDate
            GROUP BY e.DriverId
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken))).ToDictionary(r => r.DriverId, r => r.Count);

        var fuelByDriver = (await connection.QueryAsync<(int DriverId, decimal Liters)>(new CommandDefinition(
            """
            SELECT DriverId, SUM(Liters) AS Liters
            FROM FuelLogs
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND DriverId IS NOT NULL
              AND FuelDate BETWEEN @FromDate AND @ToDate
            GROUP BY DriverId
            """,
            new { TenantId = tenantId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken))).ToDictionary(r => r.DriverId, r => r.Liters);

        var idleMinutesByDriver = new Dictionary<int, int>();
        var harshCountByDriver = new Dictionary<int, int>();
        var traccarGap = false;

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled)
        {
            var deviceMap = await GpsTraccarFleetFetcher.ResolveVehicleToDeviceMapAsync(connection, tenantId, vehicleIds, cancellationToken);
            traccarGap = await GpsTraccarFleetFetcher.HasNonTraccarVehicleAsync(connection, tenantId, vehicleIds, cancellationToken);

            if (deviceMap.Count > 0)
            {
                var deviceToVehicle = deviceMap.ToDictionary(kv => kv.Value, kv => kv.Key);
                var stopsTasks = deviceMap.Values.Select(id => traccarClient.GetStopsAsync(id, fromDate, toDate, cancellationToken));
                var eventsTasks = deviceMap.Values.Select(id => traccarClient.GetEventsAsync(id, fromDate, toDate, ct: cancellationToken));

                var allStops = (await Task.WhenAll(stopsTasks)).SelectMany(s => s).ToList();
                var allEvents = (await Task.WhenAll(eventsTasks)).SelectMany(e => e).ToList();

                foreach (var stop in allStops)
                {
                    if (!deviceToVehicle.TryGetValue(stop.DeviceId, out var vehicleId)) continue;
                    var driverId = ResolveDriver(vehicleId, stop.StartTime);
                    if (!driverId.HasValue) continue;

                    var minutes = stop.Duration >= 100_000 ? stop.Duration / 60_000 : Math.Max(1, stop.Duration / 60);
                    idleMinutesByDriver[driverId.Value] = idleMinutesByDriver.GetValueOrDefault(driverId.Value) + minutes;
                }

                foreach (var evt in allEvents)
                {
                    if (!IsHarshEvent(evt.Type)) continue;
                    if (!deviceToVehicle.TryGetValue(evt.DeviceId, out var vehicleId)) continue;
                    var driverId = ResolveDriver(vehicleId, evt.EventTime);
                    if (!driverId.HasValue) continue;

                    harshCountByDriver[driverId.Value] = harshCountByDriver.GetValueOrDefault(driverId.Value) + 1;
                }
            }
        }
        else
        {
            traccarGap = true;
        }

        var results = new List<DriverScoreDto>();
        foreach (var group in attributedTrips.GroupBy(x => x.DriverId!.Value))
        {
            var driverId = group.Key;
            var driverTrips = group.Select(x => x.Trip).ToList();
            var distanceKm = (decimal)driverTrips.Sum(t => t.DistanceKm);
            var nightTrips = driverTrips.Count(t => t.StartTime.Hour >= NightStartHour || t.StartTime.Hour < NightEndHour);
            var nightPercent = driverTrips.Count > 0 ? Math.Round(nightTrips * 100m / driverTrips.Count, 1) : 0;
            var idleMinutes = idleMinutesByDriver.GetValueOrDefault(driverId);
            var drivingMinutes = driverTrips.Sum(t => t.DurationMinutes);
            var idlePercent = (drivingMinutes + idleMinutes) > 0
                ? Math.Round(idleMinutes * 100m / (drivingMinutes + idleMinutes), 1)
                : 0;

            var factors = new DriverScoreFactorsDto(
                driverTrips.Count,
                Math.Round(distanceKm, 2),
                overspeedByDriver.GetValueOrDefault(driverId),
                idleMinutes,
                harshCountByDriver.GetValueOrDefault(driverId),
                nightPercent,
                fuelByDriver.TryGetValue(driverId, out var liters) ? liters : null);

            var score = DriverScoreCalculator.ComputeScore(factors, idlePercent);

            results.Add(new DriverScoreDto(
                driverId,
                driverNames.GetValueOrDefault(driverId, $"Driver #{driverId}"),
                score,
                DriverScoreCalculator.RatingFor(score),
                factors,
                traccarGap));
        }

        return ApiResponse<List<DriverScoreDto>>.SuccessResponse(results.OrderByDescending(r => r.Score).ToList());
    }

    private static bool IsHarshEvent(string type) =>
        type.Contains("braking", StringComparison.OrdinalIgnoreCase) ||
        type.Contains("acceleration", StringComparison.OrdinalIgnoreCase);
}
