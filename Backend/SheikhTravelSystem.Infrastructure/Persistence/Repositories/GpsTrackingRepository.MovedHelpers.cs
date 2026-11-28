using System.Data;
using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Queries;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed partial class GpsTrackingRepository
{
    private const string BasisNote = "Cost per KM includes fuel and maintenance costs only. Toll, parking, and driver cost are not tracked in this system.";
    private const int NightStartHour = 22;
    private const int NightEndHour = 5;
    private const int MaxPageSize = 5000;
    private const int SparseRemoteThreshold = 5;
    private const int MaxFleetTraccarCalls = 25;
    private const int MaxFleetDetectorCalls = 10;
    private const int MaxExportRows = 50_000;
    private static readonly TimeSpan ReplayCacheTtl = TimeSpan.FromMinutes(3);
    // MaxRange already defined in GpsTrackingRepository.Helpers.cs (90-day default)

    private static bool IsHarshEvent(string type) =>
        TraccarHarshEventMapper.IsCanonicalHarsh(type) ||
        TraccarHarshEventMapper.LooksLikeHarsh(type);

    private async Task<(int Overspeed, int HarshBrake, int HarshAccel)> CountSafetyAlertsInRangeAsync(
        IReadOnlyCollection<int> vehicleIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        if (vehicleIds.Count == 0)
            return (0, 0, 0);

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var row = await connection.QuerySingleAsync<(int Overspeed, int HarshBrake, int HarshAccel)>(
            new CommandDefinition(
                """
                SELECT
                  ISNULL(SUM(CASE WHEN LOWER(e.EventType) IN ('overspeed', 'speed_exceeded', 'road_speed_exceeded') THEN 1 ELSE 0 END), 0),
                  ISNULL(SUM(CASE WHEN LOWER(e.EventType) IN ('harsh_braking', 'hardbraking', 'hard_braking', 'braking') THEN 1 ELSE 0 END), 0),
                  ISNULL(SUM(CASE WHEN LOWER(e.EventType) IN ('harsh_acceleration', 'hardacceleration', 'hard_acceleration', 'acceleration') THEN 1 ELSE 0 END), 0)
                FROM GpsAlertEvents e
                INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                WHERE e.IsDeleted = 0
                  AND e.VehicleId IN @VehicleIds
                  AND e.Timestamp >= @FromDate AND e.Timestamp < @ToDate
                """,
                new { TenantId = tenantId, VehicleIds = vehicleIds, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        return row;
    }

    private async Task<int> CountOverspeedTodayAsync(CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.EventType IN ('overspeed', 'speed_exceeded') AND e.IsDeleted = 0
              AND e.Timestamp >= @TodayStart
            """,
            new { TenantId = tenantId, TodayStart = await TenantTodayStartUtcAsync(tenantId, cancellationToken) },
            cancellationToken: cancellationToken));
    }

    private async Task<List<PositionDto>> EnrichLiveAddressesAsync(
        System.Data.IDbConnection connection,
        List<PositionDto> items,
        CancellationToken cancellationToken)
    {
        // Hot path: never call Google/Nominatim synchronously on live roster polls.
        // Queue at most one background backfill per coarse/missing address (cooldown in queue).
        foreach (var pos in items)
        {
            if (TripReplayAddressEnricher.IsCoarseAddress(pos.Address))
                addressBackfill.Enqueue(pos.VehicleId, pos.Latitude, pos.Longitude);
        }

        await Task.CompletedTask;
        return items;
    }

    private sealed record TripVehicleSourceRow(
        int VehicleId,
        string? VehicleName,
        string? PlateNumber,
        int? GpsDeviceId,
        string? DeviceName,
        int? TraccarDeviceId);

    private sealed record FleetVehicle(
        int VehicleId,
        string? VehicleName,
        string? PlateNumber,
        int? GpsDeviceId,
        string? DeviceName,
        int? TraccarDeviceId,
        string? DriverName);

    private sealed record PersistedTripRow(
        int VehicleId,
        string? VehicleName,
        string? PlateNumber,
        int? GpsDeviceId,
        string? DeviceName,
        DateTime StartTime,
        DateTime EndTime,
        decimal DistanceKm,
        decimal AvgSpeedKmh,
        decimal MaxSpeedKmh,
        int DurationMinutes);

    private static GpsTripDto ToGpsTripDto(PersistedTripRow row) => new(
        row.VehicleId,
        row.VehicleName,
        row.GpsDeviceId,
        row.StartTime,
        row.EndTime,
        (double)row.DistanceKm,
        row.AvgSpeedKmh,
        row.MaxSpeedKmh,
        row.DurationMinutes,
        DeviceName: row.DeviceName,
        PlateNumber: row.PlateNumber);

    /// <summary>
    /// GpsTrips SQL rows have no lat/lng — backfill endpoints from nearest GpsPositions.
    /// </summary>
    private async Task<List<GpsTripDto>> EnrichTripEndpointsFromPositionsAsync(
        List<GpsTripDto> trips,
        CancellationToken cancellationToken)
    {
        var needing = trips
            .Where(t => !GpsTripLocationHelper.HasUsableCoordinates(t.StartLatitude, t.StartLongitude)
                        || !GpsTripLocationHelper.HasUsableCoordinates(t.EndLatitude, t.EndLongitude))
            .ToList();
        if (needing.Count == 0) return trips;

        var vehicleIds = needing.Select(t => t.VehicleId).Distinct().ToList();
        var fromPad = needing.Min(t => t.StartTime).AddMinutes(-15);
        var toPad = needing.Max(t => t.EndTime).AddMinutes(15);

        using var connection = dbFactory.CreateConnection();
        var positions = (await connection.QueryAsync<(int VehicleId, double Latitude, double Longitude, DateTime RecordedAt, string? Address)>(
            new CommandDefinition(
                """
                SELECT VehicleId, Latitude, Longitude, RecordedAt, Address
                FROM GpsPositions
                WHERE VehicleId IN @VehicleIds
                  AND RecordedAt >= @FromPad
                  AND RecordedAt <= @ToPad
                ORDER BY VehicleId, RecordedAt
                """,
                new { VehicleIds = vehicleIds, FromPad = fromPad, ToPad = toPad },
                cancellationToken: cancellationToken))).ToList();

        if (positions.Count == 0) return trips;

        var byVehicle = positions.GroupBy(p => p.VehicleId).ToDictionary(g => g.Key, g => g.ToList());

        static (double Lat, double Lng, string? Address)? Nearest(
            IReadOnlyList<(int VehicleId, double Latitude, double Longitude, DateTime RecordedAt, string? Address)> pts,
            DateTime at)
        {
            if (pts.Count == 0) return null;
            var best = pts[0];
            var bestDelta = Math.Abs((best.RecordedAt - at).TotalSeconds);
            for (var i = 1; i < pts.Count; i++)
            {
                var delta = Math.Abs((pts[i].RecordedAt - at).TotalSeconds);
                if (delta < bestDelta)
                {
                    best = pts[i];
                    bestDelta = delta;
                }
            }

            // Ignore points more than 30 minutes away from the trip endpoint.
            if (bestDelta > 30 * 60) return null;
            if (!GpsTripLocationHelper.HasUsableCoordinates(best.Latitude, best.Longitude)) return null;
            return (best.Latitude, best.Longitude, best.Address);
        }

        var result = new List<GpsTripDto>(trips.Count);
        foreach (var trip in trips)
        {
            var startLat = trip.StartLatitude;
            var startLng = trip.StartLongitude;
            var endLat = trip.EndLatitude;
            var endLng = trip.EndLongitude;
            var startAddress = trip.StartAddress;
            var endAddress = trip.EndAddress;

            if (!byVehicle.TryGetValue(trip.VehicleId, out var pts))
            {
                result.Add(trip);
                continue;
            }

            if (!GpsTripLocationHelper.HasUsableCoordinates(startLat, startLng))
            {
                var start = Nearest(pts, trip.StartTime);
                if (start is { } s)
                {
                    startLat = s.Lat;
                    startLng = s.Lng;
                    if (string.IsNullOrWhiteSpace(startAddress)) startAddress = s.Address;
                }
            }

            if (!GpsTripLocationHelper.HasUsableCoordinates(endLat, endLng))
            {
                var end = Nearest(pts, trip.EndTime);
                if (end is { } e)
                {
                    endLat = e.Lat;
                    endLng = e.Lng;
                    if (string.IsNullOrWhiteSpace(endAddress)) endAddress = e.Address;
                }
            }

            result.Add(trip with
            {
                StartLatitude = startLat,
                StartLongitude = startLng,
                EndLatitude = endLat,
                EndLongitude = endLng,
                StartAddress = startAddress,
                EndAddress = endAddress
            });
        }

        return result;
    }

    /// <summary>
    /// Names each trip's start and end from the address cache only, like the History replay. A place the
    /// cache does not know keeps what it came with and is queued for the background lookup, so the trips
    /// list never waits for the rate-limited geocoder (a 7-day list waited on up to 24 provider calls of
    /// 1–10 s each, and the mobile playback tab gives the list 45 s).
    /// </summary>
    private async Task<List<GpsTripDto>> EnrichTripAddressesAsync(
        List<GpsTripDto> trips,
        CancellationToken cancellationToken)
    {
        var result = new List<GpsTripDto>(trips.Count);

        async Task<string?> NameFromCacheAsync(double latitude, double longitude)
        {
            var formatted = TripReplayAddressEnricher.FormatResolvedAddress(
                await geocoder.GetCachedAddressAsync(latitude, longitude, cancellationToken));
            if (!string.IsNullOrWhiteSpace(formatted)) return formatted;
            addressLookups.Enqueue(latitude, longitude);
            return null;
        }

        foreach (var trip in trips)
        {
            var start = trip.StartAddress;
            var end = trip.EndAddress;

            if (TripReplayAddressEnricher.IsCoarseAddress(start)
                && trip.StartLatitude is { } startLat
                && trip.StartLongitude is { } startLng)
            {
                start = await NameFromCacheAsync(startLat, startLng) ?? start;
            }

            if (TripReplayAddressEnricher.IsCoarseAddress(end)
                && trip.EndLatitude is { } endLat
                && trip.EndLongitude is { } endLng)
            {
                // Same point as a named start: reuse it instead of a second lookup.
                var samePoint = trip.StartLatitude == endLat && trip.StartLongitude == endLng;
                end = samePoint && !TripReplayAddressEnricher.IsCoarseAddress(start)
                    ? start
                    : await NameFromCacheAsync(endLat, endLng) ?? end;
            }

            result.Add(GpsTripLocationHelper.ApplyDisplayAddresses(trip with { StartAddress = start, EndAddress = end }));
        }

        return result;
    }

    private static List<GpsTripDto> ApplyFilters(List<GpsTripDto> trips, GetGpsTripsQuery request)
    {
        IEnumerable<GpsTripDto> query = trips;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t =>
                (t.VehicleName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (t.DeviceName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (t.DriverName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (t.StartAddress?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (t.EndAddress?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (t.PlateNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (request.MinDistanceKm.HasValue)
        {
            query = query.Where(t => t.DistanceKm >= request.MinDistanceKm.Value);
        }

        if (request.MaxDistanceKm.HasValue)
        {
            query = query.Where(t => t.DistanceKm <= request.MaxDistanceKm.Value);
        }

        if (request.MinAvgSpeedKmh.HasValue)
        {
            query = query.Where(t => t.AvgSpeedKmh >= request.MinAvgSpeedKmh.Value);
        }

        if (request.MaxAvgSpeedKmh.HasValue)
        {
            query = query.Where(t => t.AvgSpeedKmh <= request.MaxAvgSpeedKmh.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(t => string.Equals(t.Status, request.Status, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    private static List<GpsTripDto> ApplySort(List<GpsTripDto> trips, string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "distance" => desc
                ? trips.OrderByDescending(t => t.DistanceKm).ToList()
                : trips.OrderBy(t => t.DistanceKm).ToList(),
            "duration" => desc
                ? trips.OrderByDescending(t => t.DurationMinutes).ToList()
                : trips.OrderBy(t => t.DurationMinutes).ToList(),
            "avgspeed" => desc
                ? trips.OrderByDescending(t => t.AvgSpeedKmh).ToList()
                : trips.OrderBy(t => t.AvgSpeedKmh).ToList(),
            "vehicle" => desc
                ? trips.OrderByDescending(t => t.VehicleName).ToList()
                : trips.OrderBy(t => t.VehicleName).ToList(),
            _ => desc
                ? trips.OrderByDescending(t => t.StartTime).ToList()
                : trips.OrderBy(t => t.StartTime).ToList()
        };
    }

    private static PagedResult<GpsTripDto> ToPage(List<GpsTripDto> trips, GetGpsTripsQuery request)
    {
        var enriched = TraccarTripMapper.EnrichAll(trips);
        if (request.Unpaged || request.PageSize <= 0)
        {
            return new PagedResult<GpsTripDto>
            {
                Items = enriched,
                TotalCount = enriched.Count,
                Page = 1,
                PageSize = enriched.Count == 0 ? 1 : enriched.Count
            };
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = Math.Min(request.PageSize, 500);
        var skip = (page - 1) * pageSize;
        return new PagedResult<GpsTripDto>
        {
            Items = enriched.Skip(skip).Take(pageSize).ToList(),
            TotalCount = enriched.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<ApiResponse<List<GpsTripDto>>> HandleSingleVehicleAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        var source = await connection.QueryFirstOrDefaultAsync<TripVehicleSourceRow>(new CommandDefinition(
            """
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   d.Id AS GpsDeviceId, d.Name AS DeviceName, d.TraccarDeviceId
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
            """,
            new { VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (source is null)
        {
            return ApiResponse<List<GpsTripDto>>.FailResponse("Vehicle not found.");
        }

        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            var traccarTrips = await traccar.GetTripsAsync(
                source.TraccarDeviceId.Value,
                fromDate,
                toDate,
                cancellationToken);

            var mapped = traccarTrips
                .Select(t => TraccarTripMapper.ToGpsTripDto(
                    t,
                    source.VehicleId,
                    source.VehicleName,
                    source.GpsDeviceId,
                    source.DeviceName,
                    source.PlateNumber))
                .OrderByDescending(t => t.StartTime)
                .ToList();

            return ApiResponse<List<GpsTripDto>>.SuccessResponse(TraccarTripMapper.EnrichAll(mapped));
        }

        if (opts.IsConfigured && opts.Enabled && !source.TraccarDeviceId.HasValue)
        {
            return ApiResponse<List<GpsTripDto>>.FailResponse(
                "This vehicle has no Traccar-linked GPS device. Install a tracker or link an existing device.");
        }

        var sql = """
            SELECT t.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber, t.GpsDeviceId, d.Name AS DeviceName,
                   t.StartTime, t.EndTime, t.DistanceKm, t.AvgSpeedKmh, t.MaxSpeedKmh, t.DurationMinutes
            FROM GpsTrips t
            LEFT JOIN Vehicles v ON v.Id = t.VehicleId
            LEFT JOIN GpsDevices d ON d.Id = t.GpsDeviceId AND d.IsDeleted = 0
            WHERE t.VehicleId = @VehicleId
              AND t.StartTime >= @FromDate AND t.EndTime <= @ToDate
            ORDER BY t.EndTime DESC
            """;

        var persisted = (await connection.QueryAsync<PersistedTripRow>(new CommandDefinition(
            sql,
            new { FromDate = fromDate, ToDate = toDate, VehicleId = vehicleId },
            cancellationToken: cancellationToken))).Select(ToGpsTripDto).ToList();

        if (persisted.Count > 0)
        {
            return ApiResponse<List<GpsTripDto>>.SuccessResponse(TraccarTripMapper.EnrichAll(persisted));
        }

        var history = await mediator.Send(
            new GetPositionHistoryQuery(vehicleId, fromDate, toDate),
            cancellationToken);

        if (!history.Success || history.Data is null || history.Data.Count < 2)
        {
            return ApiResponse<List<GpsTripDto>>.SuccessResponse([]);
        }

        var trips = GpsTripDetector.DetectTrips(
            source.VehicleId,
            source.VehicleName,
            source.GpsDeviceId,
            history.Data);

        return ApiResponse<List<GpsTripDto>>.SuccessResponse(
            TraccarTripMapper.EnrichAll(trips.OrderByDescending(t => t.StartTime).ToList()));
    }

    private async Task<ApiResponse<List<GpsTripDto>>> HandleFleetWideAsync(
        System.Data.IDbConnection connection,
        int tenantId,
        GetGpsTripsQuery request,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        var filters = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0" };
        var parameters = new DynamicParameters();
        parameters.Add("TenantId", tenantId);

        if (currentUser.UserId is int userId)
        {
            var scope = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);
            if (!DataScopeSql.TryIntersectOptional(scope, request.BranchId, request.DepartmentId, out _, out _, out var scopeError))
                return ApiResponse<List<GpsTripDto>>.FailResponse(scopeError ?? "Outside data scope.");

            DataScopeSqlBuilder.ApplyVehicleScope(parameters, scope, "v", filters, request.BranchId, request.DepartmentId);
        }
        else
        {
            if (request.BranchId.HasValue)
            {
                filters.Add("v.BranchId = @BranchId");
                parameters.Add("BranchId", request.BranchId.Value);
            }

            if (request.DepartmentId.HasValue)
            {
                filters.Add("v.DepartmentId = @DepartmentId");
                parameters.Add("DepartmentId", request.DepartmentId.Value);
            }
        }

        if (request.DriverId.HasValue)
        {
            filters.Add("assignDrv.DriverId = @DriverId");
            parameters.Add("DriverId", request.DriverId.Value);
        }

        var whereClause = string.Join(" AND ", filters);

        var vehicles = (await connection.QueryAsync<FleetVehicle>(new CommandDefinition(
            $"""
            SELECT v.Id AS VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber,
                   d.Id AS GpsDeviceId, d.Name AS DeviceName, d.TraccarDeviceId,
                   assignDrv.DriverName
            FROM Vehicles v
            LEFT JOIN GpsDevices d ON d.Id = v.GpsDeviceId AND d.IsDeleted = 0
            OUTER APPLY (
                SELECT TOP 1 a.DriverId, dr.FullName AS DriverName
                FROM AssignmentHistory a
                INNER JOIN Drivers dr ON dr.Id = a.DriverId AND dr.IsDeleted = 0
                WHERE a.VehicleId = v.Id AND a.IsDeleted = 0
                  AND a.Status IN (N'Active', N'Scheduled') AND a.DriverId IS NOT NULL
                ORDER BY CASE WHEN a.Status = N'Active' THEN 0 ELSE 1 END, a.StartAt DESC
            ) assignDrv
            WHERE {whereClause}
            """,
            parameters,
            cancellationToken: cancellationToken))).ToList();

        if (vehicles.Count == 0)
        {
            return ApiResponse<List<GpsTripDto>>.SuccessResponse([]);
        }

        var vehicleInfoById = vehicles.ToDictionary(v => v.VehicleId);
        var results = new List<GpsTripDto>();
        var covered = new HashSet<int>();

        // Prefer Traccar for linked vehicles (matches single-vehicle path). Local GpsTrips has no
        // lat/lng columns — using it first left fleet rows as "Location unavailable".
        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled)
        {
            var traccarCandidates = vehicles
                .Where(v => v.TraccarDeviceId.HasValue)
                .Take(MaxFleetTraccarCalls)
                .ToList();
            if (traccarCandidates.Count > 0)
            {
                var traccarResults = await Task.WhenAll(traccarCandidates.Select(async v =>
                {
                    var trips = await traccar.GetTripsAsync(v.TraccarDeviceId!.Value, fromDate, toDate, cancellationToken);
                    return (v.VehicleId, Trips: trips.Select(t => TraccarTripMapper.ToGpsTripDto(
                        t, v.VehicleId, v.VehicleName, v.GpsDeviceId, v.DeviceName, v.PlateNumber) with
                    {
                        DriverName = t.DriverName ?? v.DriverName
                    }).ToList());
                }));

                foreach (var (vehicleId, trips) in traccarResults)
                {
                    if (trips.Count == 0) continue;
                    results.AddRange(TraccarTripMapper.EnrichAll(trips));
                    covered.Add(vehicleId);
                }
            }
        }

        // Local GpsTrips for vehicles not already covered by Traccar (or Traccar returned none).
        var localVehicleIds = vehicles.Where(v => !covered.Contains(v.VehicleId)).Select(v => v.VehicleId).ToList();
        if (localVehicleIds.Count > 0)
        {
            var localTrips = (await connection.QueryAsync<PersistedTripRow>(new CommandDefinition(
                """
                SELECT t.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS PlateNumber, t.GpsDeviceId, d.Name AS DeviceName,
                       t.StartTime, t.EndTime, t.DistanceKm, t.AvgSpeedKmh, t.MaxSpeedKmh, t.DurationMinutes
                FROM GpsTrips t
                INNER JOIN Vehicles v ON v.Id = t.VehicleId
                LEFT JOIN GpsDevices d ON d.Id = t.GpsDeviceId AND d.IsDeleted = 0
                WHERE t.VehicleId IN @VehicleIds
                  AND t.StartTime >= @FromDate AND t.EndTime <= @ToDate
                """,
                new { VehicleIds = localVehicleIds, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).Select(ToGpsTripDto).ToList();

            if (localTrips.Count > 0)
            {
                results.AddRange(TraccarTripMapper.EnrichAll(localTrips.Select(t => vehicleInfoById.TryGetValue(t.VehicleId, out var vi)
                    ? t with { DriverName = vi.DriverName ?? t.DriverName }
                    : t)));
                foreach (var id in localTrips.Select(t => t.VehicleId))
                    covered.Add(id);
            }
        }

        // Capped local position-based detection for whatever's still uncovered.
        var uncovered = vehicles.Where(v => !covered.Contains(v.VehicleId)).ToList();
        var detectorCandidates = uncovered.Take(MaxFleetDetectorCalls).ToList();
        if (detectorCandidates.Count > 0)
        {
            var detectorVehicleIds = detectorCandidates.Select(v => v.VehicleId).ToList();
            var positions = (await connection.QueryAsync<GpsPositionHistoryRow>(new CommandDefinition(
                """
                SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                       Heading, Altitude, Ignition, RecordedAt AS Timestamp, Address
                FROM GpsPositions
                WHERE VehicleId IN @VehicleIds
                  AND RecordedAt >= @FromDate
                  AND RecordedAt < @ToExclusive
                ORDER BY VehicleId, RecordedAt ASC
                """,
                new { VehicleIds = detectorVehicleIds, FromDate = fromDate, ToExclusive = toDate.AddTicks(1) },
                cancellationToken: cancellationToken)))
                .Select(GpsPositionHistoryMapper.ToPositionDto)
                .ToList();

            // GpsTripDetector has no internal grouping — feeding it mixed-vehicle positions would
            // silently splice unrelated vehicles' points into fake trips, so group first.
            var positionsByVehicle = positions.GroupBy(p => p.VehicleId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var v in detectorCandidates)
            {
                if (!positionsByVehicle.TryGetValue(v.VehicleId, out var vehiclePositions) || vehiclePositions.Count < 2)
                {
                    continue;
                }

                results.AddRange(TraccarTripMapper.EnrichAll(GpsTripDetector.DetectTrips(v.VehicleId, v.VehicleName, v.GpsDeviceId, vehiclePositions)));
            }
        }

        return ApiResponse<List<GpsTripDto>>.SuccessResponse(
            results.OrderByDescending(t => t.StartTime).ToList());
    }

    private async Task<ApiResponse<HistoryReplayBundleDto>> BuildReplayResponseAsync(
        VehicleTripSource source,
        TripDeviceContextDto vehicleContext,
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        int? routeMaxPoints,
        int? playbackMaxPoints,
        bool includeRaw,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var timings = new ReplayTimings();
        IReadOnlyDictionary<int, string>? geofenceNames = null;
        var opts = traccarOptions.Value;
        TripReplayBundleDto bundle;
        var fromTraccar = false;
        // Every fix of the window, before the route is thinned for drawing.
        var fullRoute = new List<TripReplayPositionDto>();

        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            fromTraccar = true;
            geofenceNames = await LoadGeofenceNamesAsync(traccar, cancellationToken);
            // No Traccar stops report: the replay's stops are the timeline's (HistoryReplayAssembler).
            bundle = await TripReplayLoader.LoadFromTraccarAsync(
                traccar,
                source.TraccarDeviceId.Value,
                fromDate,
                toDate,
                cancellationToken,
                geofenceNames,
                routeMaxPoints,
                playbackMaxPoints,
                includeRaw,
                fullRoute,
                includeStops: false,
                timings);
        }
        else
        {
            bundle = await TripReplayLoader.LoadFromLocalHistoryAsync(
                mediator, vehicleId, fromDate, toDate, cancellationToken,
                routeMaxPoints, playbackMaxPoints, includeRaw, fullRoute);
        }

        if (bundle.Route.Count == 0 && bundle.Playback.Count == 0)
        {
            // Traccar reports can lag or fail while sync keeps local history warm.
            fromTraccar = false;
            bundle = await TripReplayLoader.LoadFromLocalHistoryAsync(
                mediator, vehicleId, fromDate, toDate, cancellationToken,
                routeMaxPoints, playbackMaxPoints, includeRaw, fullRoute);
        }

        if (bundle.Route.Count == 0 && bundle.Playback.Count == 0)
            return ApiResponse<HistoryReplayBundleDto>.FailResponse("No tracking points in this period.");

        timings.LoadMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        // Timeline, its stops, names from the address cache (unknown places are looked up after the
        // response), statistics and display fields. TripReplayLoader already fetched the Traccar
        // summary for the window — do not re-query trips/summary here (60s+ on busy days).
        var replay = await HistoryReplayAssembler.AssembleAsync(
            new HistoryReplayInput(
                bundle,
                fullRoute,
                fromTraccar,
                vehicleId,
                fromDate,
                toDate,
                timeZone,
                DateTime.UtcNow,
                vehicleContext,
                source.TraccarDeviceId,
                source.DeviceName ?? source.VehicleName),
            geocoder,
            addressLookups,
            logger,
            cancellationToken,
            timings);

        // One line per build (cache hits are not logged). Counts and timings only: no places, no coordinates.
        logger.LogInformation(
            "History replay for vehicle {VehicleId}: {WindowHours:0.#} h, {Fixes} fixes, load {LoadMs} ms (route {RouteMs} ms, events {EventsMs} ms, summary {SummaryMs} ms), timeline {TimelineMs} ms, addresses {AddressesMs} ms ({AddressesNamed} named, {AddressesQueued} queued), total {TotalMs} ms",
            vehicleId,
            (toDate - fromDate).TotalHours,
            fullRoute.Count > 0 ? fullRoute.Count : bundle.Route.Count,
            timings.LoadMs,
            timings.RouteMs,
            timings.EventsMs,
            timings.SummaryMs,
            timings.TimelineMs,
            timings.AddressesMs,
            timings.Addresses.Named,
            timings.Addresses.Queued,
            (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        return ApiResponse<HistoryReplayBundleDto>.SuccessResponse(replay);
    }

    private static async Task<IReadOnlyDictionary<int, string>> LoadGeofenceNamesAsync(
        ITraccarClient traccar,
        CancellationToken cancellationToken)
    {
        try
        {
            var geofences = await traccar.GetGeofencesAsync(cancellationToken);
            return geofences.ToDictionary(g => g.Id, g => g.Name);
        }
        catch
        {
            return new Dictionary<int, string>();
        }
    }

    private static async Task<List<PositionDto>> LoadFullPositionsAsync(
        IDbConnectionFactory dbFactory,
        ITraccarClient traccar,
        TraccarOptions opts,
        int tenantId,
        int vehicleId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var toExclusive = toDate.AddTicks(1);

        if (opts.Enabled)
        {
            var link = await connection.QuerySingleOrDefaultAsync<(int? GpsDeviceId, int? TraccarDeviceId)>(
                new CommandDefinition(
                    """
                    SELECT TOP 1 d.Id AS GpsDeviceId, d.TraccarDeviceId
                    FROM GpsDevices d
                    INNER JOIN Vehicles v ON v.Id = d.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
                    WHERE d.VehicleId = @VehicleId AND d.IsDeleted = 0 AND d.TraccarDeviceId IS NOT NULL
                      AND (d.TenantId = v.TenantId OR d.TenantId IS NULL)
                    ORDER BY d.Id DESC
                    """,
                    new { VehicleId = vehicleId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (link.TraccarDeviceId is int traccarDeviceId)
            {
                var route = await traccar.GetRouteAsync(traccarDeviceId, fromDate, toDate, cancellationToken);
                if (route.Count < SparseRemoteThreshold)
                {
                    var remotePositions = await traccar.GetPositionsByDeviceAsync(
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
            @"SELECT p.Id, p.VehicleId, p.DriverId, p.BookingId, p.GpsDeviceId, p.Latitude, p.Longitude, p.Speed,
                     p.Heading, p.Altitude, p.Ignition, p.RecordedAt AS Timestamp, p.Address
              FROM GpsPositions p
              INNER JOIN Vehicles v ON v.Id = p.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
              WHERE p.VehicleId = @VehicleId
                AND p.RecordedAt >= @FromDate
                AND p.RecordedAt < @ToExclusive
              ORDER BY p.RecordedAt ASC",
            new { VehicleId = vehicleId, TenantId = tenantId, FromDate = fromDate, ToExclusive = toExclusive },
            cancellationToken: cancellationToken));

        return localRows.Select(GpsPositionHistoryMapper.ToPositionDto).ToList();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
            return value;
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

}
