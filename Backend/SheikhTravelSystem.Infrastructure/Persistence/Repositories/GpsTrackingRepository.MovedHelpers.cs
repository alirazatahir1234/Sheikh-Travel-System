using System.Data;
using System.Globalization;
using System.Text;
using Dapper;
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
    private const int MaxInlineEnrich = 40;
    private const int SparseRemoteThreshold = 5;
    private const int MaxFleetTraccarCalls = 25;
    private const int MaxFleetDetectorCalls = 10;
    private const int MaxExportRows = 50_000;
    private static readonly TimeSpan ReplayCacheTtl = TimeSpan.FromMinutes(3);
    // MaxRange already defined in GpsTrackingRepository.Helpers.cs (90-day default)

    private static bool IsHarshEvent(string type) =>
        type.Contains("braking", StringComparison.OrdinalIgnoreCase) ||
        type.Contains("acceleration", StringComparison.OrdinalIgnoreCase);

    private async Task<int> CountOverspeedTodayAsync(CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1) FROM GpsAlertEvents e
            INNER JOIN Vehicles v ON v.Id = e.VehicleId AND v.TenantId = @TenantId
            WHERE e.EventType IN ('overspeed', 'speed_exceeded') AND e.IsDeleted = 0
              AND e.Timestamp >= CAST(GETUTCDATE() AS DATE)
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    private async Task<List<PositionDto>> EnrichLiveAddressesAsync(
        System.Data.IDbConnection connection,
        List<PositionDto> items,
        CancellationToken cancellationToken)
    {
        var needEnrich = items
            .Select((p, i) => (p, i))
            .Where(x => TripReplayAddressEnricher.IsCoarseAddress(x.p.Address))
            .ToList();
        if (needEnrich.Count == 0) return items;

        var inline = needEnrich.Take(MaxInlineEnrich).ToList();
        foreach (var (leftover, _) in needEnrich.Skip(MaxInlineEnrich))
            addressBackfill.Enqueue(leftover.VehicleId, leftover.Latitude, leftover.Longitude);

        for (var n = 0; n < inline.Count; n++)
        {
            var (pos, idx) = inline[n];
            try
            {
                var coarse = !string.IsNullOrWhiteSpace(pos.Address);
                var result = await geocoder.GetAddressAsync(
                    pos.Latitude,
                    pos.Longitude,
                    forceRefresh: coarse,
                    cancellationToken);
                var formatted = TripReplayAddressEnricher.FormatResolvedAddress(result);
                if (string.IsNullOrWhiteSpace(formatted))
                {
                    addressBackfill.Enqueue(pos.VehicleId, pos.Latitude, pos.Longitude);
                    continue;
                }

                items[idx] = pos with { Address = formatted };

                if (!string.Equals(pos.Address, formatted, StringComparison.Ordinal))
                {
                    await connection.ExecuteAsync(new CommandDefinition("""
                        UPDATE VehicleCurrentLocation
                        SET Address = @Address
                        WHERE VehicleId = @VehicleId
                        """,
                        new { VehicleId = pos.VehicleId, Address = formatted },
                        cancellationToken: cancellationToken));
                }
            }
            catch
            {
                addressBackfill.Enqueue(pos.VehicleId, pos.Latitude, pos.Longitude);
            }
        }

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

    private async Task<List<GpsTripDto>> EnrichTripAddressesAsync(
        List<GpsTripDto> trips,
        CancellationToken cancellationToken)
    {
        const int maxLookups = 24;
        var lookups = 0;
        var result = new List<GpsTripDto>(trips.Count);

        foreach (var trip in trips)
        {
            if (lookups >= maxLookups)
            {
                result.Add(trip);
                continue;
            }

            var start = trip.StartAddress;
            var end = trip.EndAddress;
            var startNeeds = TripReplayAddressEnricher.IsCoarseAddress(start)
                             && trip.StartLatitude is not null
                             && trip.StartLongitude is not null;
            var endNeeds = TripReplayAddressEnricher.IsCoarseAddress(end)
                           && trip.EndLatitude is not null
                           && trip.EndLongitude is not null;

            if (startNeeds)
            {
                var resolved = await geocoder.GetAddressAsync(
                    trip.StartLatitude!.Value,
                    trip.StartLongitude!.Value,
                    forceRefresh: false,
                    cancellationToken);
                var formatted = TripReplayAddressEnricher.FormatResolvedAddress(resolved);
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    start = formatted;
                }

                lookups++;
            }

            if (endNeeds && lookups < maxLookups)
            {
                // Same point → reuse start (avoids a second provider call).
                if (startNeeds
                    && trip.StartLatitude == trip.EndLatitude
                    && trip.StartLongitude == trip.EndLongitude
                    && !string.IsNullOrWhiteSpace(start))
                {
                    end = start;
                }
                else
                {
                    var resolved = await geocoder.GetAddressAsync(
                        trip.EndLatitude!.Value,
                        trip.EndLongitude!.Value,
                        forceRefresh: false,
                        cancellationToken);
                    var formatted = TripReplayAddressEnricher.FormatResolvedAddress(resolved);
                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        end = formatted;
                    }

                    lookups++;
                }
            }

            result.Add(trip with { StartAddress = start, EndAddress = end });
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
        var vehicleIds = vehicles.Select(v => v.VehicleId).ToList();
        var results = new List<GpsTripDto>();

        // Step 1: local GpsTrips table across the whole filtered vehicle set.
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
            new { VehicleIds = vehicleIds, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken))).Select(ToGpsTripDto).ToList();

        results.AddRange(TraccarTripMapper.EnrichAll(localTrips.Select(t => vehicleInfoById.TryGetValue(t.VehicleId, out var vi)
            ? t with { DriverName = vi.DriverName ?? t.DriverName }
            : t)));

        var covered = localTrips.Select(t => t.VehicleId).ToHashSet();
        var uncovered = vehicles.Where(v => !covered.Contains(v.VehicleId)).ToList();

        // Step 2: capped Traccar fallback, only for uncovered + Traccar-linked vehicles.
        var opts = traccarOptions.Value;
        if (opts.IsConfigured && opts.Enabled && uncovered.Count > 0)
        {
            var traccarCandidates = uncovered.Where(v => v.TraccarDeviceId.HasValue).Take(MaxFleetTraccarCalls).ToList();
            if (traccarCandidates.Count > 0)
            {
                var traccarResults = await Task.WhenAll(traccarCandidates.Select(async v =>
                {
                    var trips = await traccar.GetTripsAsync(v.TraccarDeviceId!.Value, fromDate, toDate, cancellationToken);
                    return trips.Select(t => TraccarTripMapper.ToGpsTripDto(
                        t, v.VehicleId, v.VehicleName, v.GpsDeviceId, v.DeviceName, v.PlateNumber) with
                    {
                        DriverName = t.DriverName ?? v.DriverName
                    });
                }));

                results.AddRange(TraccarTripMapper.EnrichAll(traccarResults.SelectMany(r => r)));
                var traccarCovered = traccarCandidates.Select(v => v.VehicleId).ToHashSet();
                uncovered = uncovered.Where(v => !traccarCovered.Contains(v.VehicleId)).ToList();
            }
        }

        // Step 3: capped local position-based detection for whatever's still uncovered.
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
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, string>? geofenceNames = null;
        var opts = traccarOptions.Value;
        TripReplayBundleDto bundle;

        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            geofenceNames = await LoadGeofenceNamesAsync(traccar, cancellationToken);
            bundle = await TripReplayLoader.LoadFromTraccarAsync(
                traccar,
                source.TraccarDeviceId.Value,
                fromDate,
                toDate,
                cancellationToken,
                geofenceNames,
                routeMaxPoints,
                playbackMaxPoints,
                includeRaw);
        }
        else
        {
            bundle = await TripReplayLoader.LoadFromLocalHistoryAsync(
                mediator, vehicleId, fromDate, toDate, cancellationToken,
                routeMaxPoints, playbackMaxPoints, includeRaw);
        }

        if (bundle.Route.Count == 0 && bundle.Playback.Count == 0)
            return ApiResponse<HistoryReplayBundleDto>.FailResponse("No tracking points in this period.");

        bundle = await TripReplayAddressEnricher.EnrichAsync(
            bundle,
            geocoder,
            cancellationToken);

        TripAnalyticsSummaryDto? rawStats = null;
        if (opts.IsConfigured && opts.Enabled && source.TraccarDeviceId.HasValue)
        {
            var deviceId = source.TraccarDeviceId.Value;
            var summaryTask = traccar.GetSummaryAsync(deviceId, fromDate, toDate, cancellationToken);
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
            HistoryReplayMapper.WithDisplayFields(
                new HistoryReplayBundleDto(
                    bundle.Route,
                    bundle.Playback,
                    bundle.Stops,
                    bundle.Events,
                    summary,
                    statistics,
                    mileageKm,
                    vehicleContext),
                source.TraccarDeviceId,
                source.DeviceName ?? source.VehicleName,
                fromDate,
                toDate));
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
                    WHERE d.VehicleId = @VehicleId AND d.IsDeleted = 0 AND d.TraccarDeviceId IS NOT NULL
                    ORDER BY d.Id DESC
                    """,
                    new { VehicleId = vehicleId },
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
            @"SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                     Heading, Altitude, Ignition, RecordedAt AS Timestamp, Address
              FROM GpsPositions
              WHERE VehicleId = @VehicleId
                AND RecordedAt >= @FromDate
                AND RecordedAt < @ToExclusive
              ORDER BY RecordedAt ASC",
            new { VehicleId = vehicleId, FromDate = fromDate, ToExclusive = toExclusive },
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
