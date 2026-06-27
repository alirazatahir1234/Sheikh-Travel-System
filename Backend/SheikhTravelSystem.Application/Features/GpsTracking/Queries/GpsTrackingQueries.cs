using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetLivePositionsQuery : IRequest<ApiResponse<List<PositionDto>>>;

public class GetLivePositionsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetLivePositionsQuery, ApiResponse<List<PositionDto>>>
{
    public async Task<ApiResponse<List<PositionDto>>> Handle(GetLivePositionsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var rows = await connection.QueryAsync<PositionDto>(new CommandDefinition(
            @"SELECT CAST(vcl.VehicleId AS BIGINT) AS Id,
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
                     vcl.LastUpdate AS Timestamp
              FROM VehicleCurrentLocation vcl
              INNER JOIN Vehicles v ON v.Id = vcl.VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0
              WHERE vcl.Latitude IS NOT NULL
                AND vcl.Longitude IS NOT NULL
                AND NOT (vcl.Latitude = 0 AND vcl.Longitude = 0)",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));

        return ApiResponse<List<PositionDto>>.SuccessResponse(rows.ToList());
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

public record GetGpsTripsQuery(int? VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<GpsTripDto>>>;

public class GetGpsTripsQueryHandler(IDbConnectionFactory dbFactory, IMediator mediator)
    : IRequestHandler<GetGpsTripsQuery, ApiResponse<List<GpsTripDto>>>
{
    public async Task<ApiResponse<List<GpsTripDto>>> Handle(GetGpsTripsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-7);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var sql = """
            SELECT t.VehicleId, v.Name AS VehicleName, t.GpsDeviceId, t.StartTime, t.EndTime,
                   t.DistanceKm, t.AvgSpeedKmh, t.MaxSpeedKmh, t.DurationMinutes
            FROM GpsTrips t
            LEFT JOIN Vehicles v ON v.Id = t.VehicleId
            WHERE t.StartTime >= @FromDate AND t.EndTime <= @ToDate
            """;

        if (request.VehicleId.HasValue)
        {
            sql += " AND t.VehicleId = @VehicleId";
        }

        sql += " ORDER BY t.EndTime DESC";

        var persisted = (await connection.QueryAsync<GpsTripDto>(new CommandDefinition(
            sql,
            new { FromDate = fromDate, ToDate = toDate, request.VehicleId },
            cancellationToken: cancellationToken))).ToList();

        if (persisted.Count > 0)
        {
            return ApiResponse<List<GpsTripDto>>.SuccessResponse(persisted);
        }

        var vehicleIds = request.VehicleId.HasValue
            ? new List<int> { request.VehicleId.Value }
            : (await connection.QueryAsync<int>(new CommandDefinition(
                "SELECT DISTINCT VehicleId FROM GpsPositions",
                cancellationToken: cancellationToken))).ToList();

        var trips = new List<GpsTripDto>();
        foreach (var vehicleId in vehicleIds)
        {
            var history = await mediator.Send(
                new GetPositionHistoryQuery(vehicleId, request.FromDate, request.ToDate),
                cancellationToken);

            if (!history.Success || history.Data is null || history.Data.Count < 2)
            {
                continue;
            }

            var vehicleName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT Name FROM Vehicles WHERE Id = @Id",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));

            var deviceId = history.Data.LastOrDefault()?.GpsDeviceId;
            trips.AddRange(GpsTripDetector.DetectTrips(vehicleId, vehicleName, deviceId, history.Data));
        }

        return ApiResponse<List<GpsTripDto>>.SuccessResponse(
            trips.OrderByDescending(t => t.StartTime).ToList());
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
