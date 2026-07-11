using Dapper;
using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetLivePositionsQuery(int Page = 1, int PageSize = 500) : IRequest<ApiResponse<PagedResult<PositionDto>>>;

public class GetLivePositionsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetLivePositionsQuery, ApiResponse<PagedResult<PositionDto>>>
{
    // Higher ceiling than the generic AppConstants.MaxPageSize (100): this endpoint feeds a live
    // fleet map meant to return "everything currently visible" in as few round-trips as possible,
    // not a paged list UI, so callers can request a single generously-sized page.
    private const int MaxPageSize = 5000;

    public async Task<ApiResponse<PagedResult<PositionDto>>> Handle(GetLivePositionsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 500 : Math.Min(request.PageSize, MaxPageSize);

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        const string whereClause = """
              FROM VehicleCurrentLocation vcl
              INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
              LEFT JOIN Drivers dr ON dr.Id = vcl.DriverId AND dr.IsDeleted = 0
              WHERE vcl.Latitude IS NOT NULL
                AND vcl.Longitude IS NOT NULL
                AND NOT (vcl.Latitude = 0 AND vcl.Longitude = 0)
            """;

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) {whereClause}",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<PositionDto>(new CommandDefinition(
            $"""
              SELECT CAST(vcl.VehicleId AS BIGINT) AS Id,
                     vcl.VehicleId,
                     vcl.DriverId,
                     vcl.BookingId,
                     vcl.GpsDeviceId,
                     vcl.Latitude,
                     vcl.Longitude,
                     ISNULL(vcl.Speed, 0) AS Speed,
                     vcl.Heading,
                     CAST(NULL AS FLOAT) AS Altitude,
                     vcl.Ignition,
                     vcl.LastUpdate AS Timestamp,
                     vcl.FuelLevel,
                     vcl.BatteryLevel,
                     vcl.GsmSignal,
                     vcl.TotalDistanceKm,
                     vcl.Address,
                     vcl.AlarmType,
                     dr.Phone AS DriverPhone
              {whereClause}
              ORDER BY vcl.VehicleId
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """,
            new { TenantId = tenantId, Offset = (page - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<PositionDto>>.SuccessResponse(new PagedResult<PositionDto>
        {
            Items = rows.ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }
}

public record GetPositionHistoryQuery(int VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<PositionDto>>>;

public class GetPositionHistoryQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPositionHistoryQuery, ApiResponse<List<PositionDto>>>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(30);

    public async Task<ApiResponse<List<PositionDto>>> Handle(GetPositionHistoryQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate > toDate)
        {
            return ApiResponse<List<PositionDto>>.FailResponse("'from' must be before 'to'.");
        }

        if (toDate - fromDate > MaxRange)
        {
            return ApiResponse<List<PositionDto>>.FailResponse("Date range cannot exceed 30 days.");
        }

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PositionDto>(new CommandDefinition(
            @"SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                     Heading, Altitude, Ignition, RecordedAt AS Timestamp
              FROM GpsPositions
              WHERE VehicleId = @VehicleId AND RecordedAt BETWEEN @FromDate AND @ToDate
              ORDER BY RecordedAt ASC",
            new { request.VehicleId, FromDate = fromDate, ToDate = toDate },
            cancellationToken: cancellationToken));

        return ApiResponse<List<PositionDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetGpsTripsQuery(
    int? VehicleId,
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null,
    int Page = 1,
    int PageSize = 100,
    bool Unpaged = false,
    string? Search = null,
    string? SortBy = null,
    string? SortDir = null,
    double? MinDistanceKm = null,
    double? MaxDistanceKm = null,
    decimal? MinAvgSpeedKmh = null,
    decimal? MaxAvgSpeedKmh = null,
    string? Status = null)
    : IRequest<ApiResponse<PagedResult<GpsTripDto>>>;

public class GetGpsTripsQueryHandler(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ITraccarClient traccarClient,
    IOptions<TraccarOptions> traccarOptions,
    ITenantContext tenantContext)
    : IRequestHandler<GetGpsTripsQuery, ApiResponse<PagedResult<GpsTripDto>>>
{
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(30);

    // Fleet-wide fallback calls are capped since each is a live Traccar HTTP round trip (or a
    // GpsPositions scan) — background persistence of trips would remove the need for this cap.
    private const int MaxFleetTraccarCalls = 25;
    private const int MaxFleetDetectorCalls = 10;

    private sealed record VehicleTripSource(
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

    public async Task<ApiResponse<PagedResult<GpsTripDto>>> Handle(GetGpsTripsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        if (fromDate > toDate)
        {
            return ApiResponse<PagedResult<GpsTripDto>>.FailResponse("'from' must be before 'to'.");
        }

        if (toDate - fromDate > MaxRange)
        {
            return ApiResponse<PagedResult<GpsTripDto>>.FailResponse("Date range cannot exceed 30 days.");
        }

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        List<GpsTripDto> allTrips;
        if (request.VehicleId.HasValue)
        {
            var single = await HandleSingleVehicleAsync(connection, tenantId, request.VehicleId.Value, fromDate, toDate, cancellationToken);
            if (!single.Success || single.Data is null)
            {
                return ApiResponse<PagedResult<GpsTripDto>>.FailResponse(single.Message ?? "Failed to load trips.");
            }

            allTrips = single.Data;
        }
        else
        {
            var fleet = await HandleFleetWideAsync(connection, tenantId, request, fromDate, toDate, cancellationToken);
            if (!fleet.Success || fleet.Data is null)
            {
                return ApiResponse<PagedResult<GpsTripDto>>.FailResponse(fleet.Message ?? "Failed to load trips.");
            }

            allTrips = fleet.Data;
        }

        allTrips = ApplyFilters(allTrips, request);
        allTrips = ApplySort(allTrips, request.SortBy, request.SortDir);
        return ApiResponse<PagedResult<GpsTripDto>>.SuccessResponse(ToPage(allTrips, request));
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
        var source = await connection.QueryFirstOrDefaultAsync<VehicleTripSource>(new CommandDefinition(
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
            var traccarTrips = await traccarClient.GetTripsAsync(
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

        var persisted = (await connection.QueryAsync<GpsTripDto>(new CommandDefinition(
            sql,
            new { FromDate = fromDate, ToDate = toDate, VehicleId = vehicleId },
            cancellationToken: cancellationToken))).ToList();

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

    /// <summary>
    /// Fleet-wide trip ledger: local-data-primary, Traccar-lazy-secondary, capped. Local
    /// <c>GpsTrips</c> rows are served first (cheap, one query); only vehicles with zero local
    /// rows in range fall back to a capped, parallel Traccar fan-out; anything still uncovered
    /// falls back to capped local position-based detection, grouped per vehicle.
    /// </summary>
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
        var localTrips = (await connection.QueryAsync<GpsTripDto>(new CommandDefinition(
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
            cancellationToken: cancellationToken))).ToList();

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
                    var trips = await traccarClient.GetTripsAsync(v.TraccarDeviceId!.Value, fromDate, toDate, cancellationToken);
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
            var positions = (await connection.QueryAsync<PositionDto>(new CommandDefinition(
                """
                SELECT Id, VehicleId, DriverId, BookingId, GpsDeviceId, Latitude, Longitude, Speed,
                       Heading, Altitude, Ignition, RecordedAt AS Timestamp
                FROM GpsPositions
                WHERE VehicleId IN @VehicleIds AND RecordedAt BETWEEN @FromDate AND @ToDate
                ORDER BY VehicleId, RecordedAt ASC
                """,
                new { VehicleIds = detectorVehicleIds, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken))).ToList();

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
}

public record GetGeofencesQuery : IRequest<ApiResponse<List<GeofenceDto>>>;

public class GetGeofencesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGeofencesQuery, ApiResponse<List<GeofenceDto>>>
{
    public async Task<ApiResponse<List<GeofenceDto>>> Handle(GetGeofencesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GeofenceDto>(new CommandDefinition(
            @"SELECT Id, Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, IsActive
              FROM Geofences WHERE IsDeleted = 0 ORDER BY Name",
            cancellationToken: cancellationToken));

        return ApiResponse<List<GeofenceDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetGpsAlertRulesQuery : IRequest<ApiResponse<List<GpsAlertRuleDto>>>;

public class GetGpsAlertRulesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGpsAlertRulesQuery, ApiResponse<List<GpsAlertRuleDto>>>
{
    public async Task<ApiResponse<List<GpsAlertRuleDto>>> Handle(GetGpsAlertRulesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsAlertRuleDto>(new CommandDefinition(
            @"SELECT r.Id, r.VehicleId, v.Name AS VehicleName, r.SpeedLimitKmh, r.GeofenceId,
                     g.Name AS GeofenceName, r.AlertOnEnter, r.AlertOnExit, r.IsActive
              FROM GpsAlertRules r
              LEFT JOIN Vehicles v ON v.Id = r.VehicleId
              LEFT JOIN Geofences g ON g.Id = r.GeofenceId
              WHERE r.IsDeleted = 0 ORDER BY r.Id DESC",
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsAlertRuleDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetGpsAlertEventsQuery(int? VehicleId, bool? UnacknowledgedOnly)
    : IRequest<ApiResponse<List<GpsAlertEventDto>>>;

public class GetGpsAlertEventsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGpsAlertEventsQuery, ApiResponse<List<GpsAlertEventDto>>>
{
    public async Task<ApiResponse<List<GpsAlertEventDto>>> Handle(GetGpsAlertEventsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT e.Id, e.RuleId, e.VehicleId, v.Name AS VehicleName, e.EventType,
                   e.Latitude, e.Longitude, e.Speed, e.Message, e.Timestamp, e.IsAcknowledged
            FROM GpsAlertEvents e
            LEFT JOIN Vehicles v ON v.Id = e.VehicleId
            WHERE e.IsDeleted = 0
            """;

        if (request.VehicleId.HasValue)
        {
            sql += " AND e.VehicleId = @VehicleId";
        }

        if (request.UnacknowledgedOnly == true)
        {
            sql += " AND e.IsAcknowledged = 0";
        }

        sql += " ORDER BY e.Timestamp DESC";

        var rows = await connection.QueryAsync<GpsAlertEventDto>(new CommandDefinition(
            sql,
            new { request.VehicleId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsAlertEventDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetGpsDevicesQuery : IRequest<ApiResponse<List<GpsDeviceDto>>>;

public class GetGpsDevicesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGpsDevicesQuery, ApiResponse<List<GpsDeviceDto>>>
{
    public async Task<ApiResponse<List<GpsDeviceDto>>> Handle(GetGpsDevicesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsDeviceDto>(new CommandDefinition(
            @"SELECT d.Id, d.VehicleId,
                     CASE WHEN v.Status = 5 THEN NULL ELSE v.Name END AS VehicleName,
                     CASE WHEN v.Status = 5 OR v.RegistrationNumber LIKE 'DRAFT-%' THEN NULL
                          ELSE v.RegistrationNumber END AS PlateNumber,
                     COALESCE(drVcl.FullName, assignDrv.DriverName) AS DriverName,
                     d.UniqueId, d.Name, d.Protocol,
                     d.SupportsEngineCutoff, d.LastIgnition, d.LastSeenAt, d.IsActive,
                     CASE WHEN d.LastSeenAt IS NOT NULL AND d.LastSeenAt > DATEADD(minute, -30, GETUTCDATE())
                          THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsOnline,
                     COALESCE(d.LastSpeed, vcl.Speed) AS LastSpeed,
                     d.LastBatteryLevel, d.LastRssi,
                     d.TraccarDeviceId,
                     CASE WHEN d.TraccarDeviceId IS NOT NULL THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsTraccarLinked,
                     CASE WHEN d.UniqueId NOT LIKE '%[^0-9]%' AND LEN(d.UniqueId) BETWEEN 14 AND 20
                          THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsValidImei,
                     d.Model, d.SimNumber, d.Vendor,
                     d.SerialNumber, d.InstallationDate, d.InstalledBy, d.InstallationNotes, d.RelayOutput
              FROM GpsDevices d
              LEFT JOIN Vehicles v ON v.Id = d.VehicleId AND v.IsDeleted = 0
              LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = d.VehicleId
              LEFT JOIN Drivers drVcl ON drVcl.Id = vcl.DriverId AND drVcl.IsDeleted = 0
              OUTER APPLY (
                  SELECT TOP 1 dr.FullName AS DriverName
                  FROM AssignmentHistory a
                  INNER JOIN Drivers dr ON dr.Id = a.DriverId AND dr.IsDeleted = 0
                  WHERE a.VehicleId = d.VehicleId AND a.IsDeleted = 0
                    AND a.Status IN (N'Active', N'Scheduled') AND a.DriverId IS NOT NULL
                  ORDER BY CASE WHEN a.Status = N'Active' THEN 0 ELSE 1 END, a.StartAt DESC
              ) assignDrv
              WHERE d.IsDeleted = 0
              ORDER BY CASE WHEN v.Name IS NULL THEN 1 ELSE 0 END, v.Name, d.Name",
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsDeviceDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetDeviceCommandsQuery(int GpsDeviceId) : IRequest<ApiResponse<List<GpsDeviceCommandDto>>>;

public class GetDeviceCommandsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDeviceCommandsQuery, ApiResponse<List<GpsDeviceCommandDto>>>
{
    public async Task<ApiResponse<List<GpsDeviceCommandDto>>> Handle(GetDeviceCommandsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsDeviceCommandDto>(new CommandDefinition(
            @"SELECT c.Id, c.GpsDeviceId, d.Name AS DeviceName, c.CommandType, c.Status,
                     c.RequestedBy, c.RequestedAt, c.CompletedAt
              FROM GpsDeviceCommands c
              INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
              WHERE c.GpsDeviceId = @GpsDeviceId AND c.IsDeleted = 0
              ORDER BY c.RequestedAt DESC",
            new { request.GpsDeviceId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsDeviceCommandDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetPendingDeviceCommandsQuery(string UniqueId) : IRequest<ApiResponse<List<GpsDeviceCommandDto>>>;

public class GetPendingDeviceCommandsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPendingDeviceCommandsQuery, ApiResponse<List<GpsDeviceCommandDto>>>
{
    public async Task<ApiResponse<List<GpsDeviceCommandDto>>> Handle(GetPendingDeviceCommandsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<GpsDeviceCommandDto>(new CommandDefinition(
            @"SELECT c.Id, c.GpsDeviceId, d.Name AS DeviceName, c.CommandType, c.Status,
                     c.RequestedBy, c.RequestedAt, c.CompletedAt
              FROM GpsDeviceCommands c
              INNER JOIN GpsDevices d ON d.Id = c.GpsDeviceId
              WHERE d.UniqueId = @UniqueId AND c.Status = 'pending' AND c.IsDeleted = 0
              ORDER BY c.RequestedAt ASC",
            new { request.UniqueId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<GpsDeviceCommandDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetGpsEtaQuery(int BookingId) : IRequest<ApiResponse<GpsEtaDto>>;

public class GetGpsEtaQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGpsEtaQuery, ApiResponse<GpsEtaDto>>
{
    public async Task<ApiResponse<GpsEtaDto>> Handle(GetGpsEtaQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var booking = await connection.QueryFirstOrDefaultAsync<(int Id, int? VehicleId, string? RouteSource, double? PickupLat, double? PickupLng)>(
            new CommandDefinition(
                @"SELECT b.Id, b.VehicleId, r.Source AS RouteSource, b.PickupLat, b.PickupLng
                  FROM Bookings b
                  LEFT JOIN Routes r ON r.Id = b.RouteId
                  WHERE b.Id = @Id AND b.IsDeleted = 0",
                new { Id = request.BookingId },
                cancellationToken: cancellationToken));

        if (booking.Id == 0 || !booking.VehicleId.HasValue)
        {
            return ApiResponse<GpsEtaDto>.FailResponse("Booking not found or no vehicle assigned.");
        }

        var position = await connection.QueryFirstOrDefaultAsync<(double Lat, double Lng)>(
            new CommandDefinition(
                @"SELECT Latitude, Longitude FROM VehicleCurrentLocation WHERE VehicleId = @VehicleId",
                new { VehicleId = booking.VehicleId.Value },
                cancellationToken: cancellationToken));

        if (position.Lat == 0 && position.Lng == 0)
        {
            return ApiResponse<GpsEtaDto>.FailResponse("No GPS position available for assigned vehicle.");
        }

        var (pickupLat, pickupLng) = booking.PickupLat.HasValue && booking.PickupLng.HasValue
            ? (booking.PickupLat.Value, booking.PickupLng.Value)
            : ResolvePickupCoordinates(booking.RouteSource);

        var distanceKm = GpsGeoHelper.HaversineKm(position.Lat, position.Lng, pickupLat, pickupLng);
        var etaMinutes = distanceKm > 0 ? (int)Math.Ceiling(distanceKm / 40.0 * 60) : 0;

        var vehicleName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Name FROM Vehicles WHERE Id = @Id",
            new { Id = booking.VehicleId.Value },
            cancellationToken: cancellationToken));

        return ApiResponse<GpsEtaDto>.SuccessResponse(new GpsEtaDto(
            request.BookingId,
            booking.VehicleId.Value,
            vehicleName,
            Math.Round(distanceKm, 2),
            etaMinutes,
            position.Lat,
            position.Lng,
            pickupLat,
            pickupLng));
    }

    private static (double Lat, double Lng) ResolvePickupCoordinates(string? routeSource)
    {
        if (string.IsNullOrWhiteSpace(routeSource))
        {
            return (31.5204, 74.3587);
        }

        var source = routeSource.ToLowerInvariant();
        if (source.Contains("lahore")) return (31.5204, 74.3587);
        if (source.Contains("islamabad")) return (33.6844, 73.0479);
        if (source.Contains("sialkot")) return (32.4945, 74.5229);
        if (source.Contains("karachi")) return (24.8607, 67.0011);
        if (source.Contains("multan")) return (30.1575, 71.5249);
        return (31.5204, 74.3587);
    }
}

public record GetGeofenceBreachCountQuery : IRequest<ApiResponse<int>>;

public class GetGeofenceBreachCountQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetGeofenceBreachCountQuery, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(GetGeofenceBreachCountQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(*) FROM GpsAlertEvents
              WHERE EventType IN ('geofence_enter', 'geofence_exit') AND IsAcknowledged = 0 AND IsDeleted = 0
              AND Timestamp > DATEADD(HOUR, -24, GETUTCDATE())",
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(count);
    }
}
