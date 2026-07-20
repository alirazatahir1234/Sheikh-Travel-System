using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.Commands;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverTripsQuery : IRequest<ApiResponse<List<DriverTripDto>>>;

public class GetDriverProfileQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverProfileQuery, ApiResponse<DriverProfileDto>>
{
    public async Task<ApiResponse<DriverProfileDto>> Handle(GetDriverProfileQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<DriverProfileDto>.FailResponse("Driver identity required.");

        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<DriverProfileDto>(new CommandDefinition(
            @"SELECT d.Id, d.FullName, d.Phone, d.Email, d.PhotoUrl, d.DriverCode,
                     d.LicenseNumber, d.LicenseExpiryDate, d.Status,
                     CASE d.Status
                       WHEN 1 THEN 'Available' WHEN 2 THEN 'On Trip' WHEN 3 THEN 'Off Duty'
                       WHEN 4 THEN 'Suspended' WHEN 5 THEN 'On Leave' ELSE 'Unknown' END AS StatusName,
                     d.IsActive,
                     v.Name AS CurrentVehicleName, v.RegistrationNumber AS CurrentVehiclePlate,
                     b.Name AS BranchName,
                     d.Rating, d.YearsExperience, d.VerificationStatus
              FROM Drivers d
              LEFT JOIN Vehicles v ON v.Id = (
                  SELECT TOP 1 VehicleId FROM Bookings
                  WHERE DriverId = d.Id AND Status IN (2,3) AND IsDeleted = 0
                  ORDER BY PickupTime DESC)
              LEFT JOIN Branches b ON b.Id = d.BranchId
              WHERE d.Id = @DriverId AND d.TenantId = @TenantId AND d.IsDeleted = 0",
            new { DriverId = driverId.Value, TenantId = tenantContext.GetRequiredTenantId() },
            cancellationToken: cancellationToken));

        if (row is null) return ApiResponse<DriverProfileDto>.FailResponse("Driver not found.");
        return ApiResponse<DriverProfileDto>.SuccessResponse(row);
    }
}

public class GetDriverDashboardQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverDashboardQuery, ApiResponse<DriverDashboardDto>>
{
    public async Task<ApiResponse<DriverDashboardDto>> Handle(GetDriverDashboardQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<DriverDashboardDto>.FailResponse("Driver identity required.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var today = DateTime.UtcNow.Date;

        var assigned = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Bookings WHERE DriverId=@D AND TenantId=@T AND Status IN (2,3) AND IsDeleted=0 AND CAST(PickupTime AS DATE)=@Today",
            new { D = driverId.Value, T = tenantId, Today = today }, cancellationToken: cancellationToken));

        var completed = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Bookings WHERE DriverId=@D AND TenantId=@T AND Status=4 AND IsDeleted=0 AND CAST(PickupTime AS DATE)=@Today",
            new { D = driverId.Value, T = tenantId, Today = today }, cancellationToken: cancellationToken));

        var clockedIn = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM DriverAttendance
                WHERE DriverId=@D AND TenantId=@T AND IsDeleted=0 AND AttendanceDate=@Today
                  AND CheckInAt IS NOT NULL AND CheckOutAt IS NULL
            ) THEN 1 ELSE 0 END",
            new { D = driverId.Value, T = tenantId, Today = today }, cancellationToken: cancellationToken));

        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var earnings = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(p.Amount),0) FROM Payments p
              INNER JOIN Bookings b ON b.Id=p.BookingId
              WHERE b.DriverId=@D AND b.IsDeleted=0 AND p.IsDeleted=0 AND b.PickupTime>=@WeekStart",
            new { D = driverId.Value, WeekStart = weekStart }, cancellationToken: cancellationToken));

        var unread = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Notifications WHERE UserId=@UserId AND IsRead=0 AND IsDeleted=0",
            new { UserId = currentUser.UserId }, cancellationToken: cancellationToken));

        var statusRow = await connection.QuerySingleOrDefaultAsync<(string? Vehicle, string? Plate, int Status)>(new CommandDefinition(
            @"SELECT v.Name, v.RegistrationNumber, d.Status FROM Drivers d
              LEFT JOIN Vehicles v ON v.Id=(
                  SELECT TOP 1 VehicleId FROM Bookings WHERE DriverId=d.Id AND Status IN(2,3) AND IsDeleted=0 ORDER BY PickupTime DESC)
              WHERE d.Id=@D AND d.IsDeleted=0",
            new { D = driverId.Value }, cancellationToken: cancellationToken));

        var statusName = statusRow.Status switch
        {
            1 => "Available", 2 => "On Trip", 3 => "Off Duty", 4 => "Suspended", 5 => "On Leave", _ => "Unknown"
        };

        return ApiResponse<DriverDashboardDto>.SuccessResponse(new DriverDashboardDto(
            assigned, completed, clockedIn,
            statusRow.Vehicle, statusRow.Plate,
            earnings, unread, statusName));
    }
}

public class GetDriverAttendanceHistoryQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverAttendanceHistoryQuery, ApiResponse<List<DriverAttendanceRecordDto>>>
{
    public async Task<ApiResponse<List<DriverAttendanceRecordDto>>> Handle(GetDriverAttendanceHistoryQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue) return ApiResponse<List<DriverAttendanceRecordDto>>.FailResponse("Driver identity required.");

        var from = request.From ?? DateTime.UtcNow.AddDays(-30);
        var to = request.To ?? DateTime.UtcNow;
        var offset = (request.Page - 1) * request.PageSize;

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<DriverAttendanceRecordDto>(new CommandDefinition(
            @"SELECT Id,
                     COALESCE(AttendanceType,
                       CASE WHEN CheckOutAt IS NOT NULL AND (CheckInAt IS NULL OR CheckOutAt >= CheckInAt) THEN N'CheckOut'
                            ELSE N'CheckIn' END) AS AttendanceType,
                     COALESCE(RecordedAt, CheckOutAt, CheckInAt, CreatedAt) AS RecordedAt,
                     Latitude, Longitude, Notes
              FROM DriverAttendance
              WHERE DriverId=@D AND IsDeleted=0
                AND COALESCE(RecordedAt, CheckOutAt, CheckInAt, CreatedAt) BETWEEN @From AND @To
              ORDER BY COALESCE(RecordedAt, CheckOutAt, CheckInAt, CreatedAt) DESC
              OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY",
            new { D = driverId.Value, From = from, To = to, Offset = offset, Size = request.PageSize },
            cancellationToken: cancellationToken));

        return ApiResponse<List<DriverAttendanceRecordDto>>.SuccessResponse(rows.ToList());
    }
}

public class GetDriverTripsQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverTripsQuery, ApiResponse<List<DriverTripDto>>>
{
    public async Task<ApiResponse<List<DriverTripDto>>> Handle(GetDriverTripsQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<List<DriverTripDto>>.FailResponse("Driver identity required.");

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var results = new List<DriverTripDto>();

        // 1) Operational Trips module (ERP lifecycle)
        var tripRows = await connection.QueryAsync<OpTripRow>(new CommandDefinition(
            @"SELECT t.Id, t.TripNumber, t.BookingId, b.BookingNumber,
                     c.FullName AS CustomerName,
                     COALESCE(r.Source + N' -> ' + r.Destination, t.TripName) AS RouteName,
                     t.PlannedStart AS PickupTime, t.PlannedEnd AS DropoffTime,
                     t.Status, t.VehicleId, v.Name AS VehicleName,
                     ISNULL(b.TotalAmount, 0) AS TotalAmount,
                     t.PickupAddress, t.PickupLatitude, t.PickupLongitude,
                     t.DestinationAddress AS DropoffAddress,
                     t.DestinationLatitude AS DropLatitude,
                     t.DestinationLongitude AS DropLongitude
              FROM Trips t
              LEFT JOIN Bookings b ON b.Id = t.BookingId
              LEFT JOIN Customers c ON c.Id = t.CustomerId
              LEFT JOIN Routes r ON r.Id = t.RouteId
              LEFT JOIN Vehicles v ON v.Id = t.VehicleId
              WHERE t.DriverId = @DriverId AND t.TenantId = @TenantId AND t.IsDeleted = 0
                AND t.Status NOT IN (@Draft, @Completed, @Cancelled, @Failed)
              ORDER BY t.PlannedStart ASC",
            new
            {
                DriverId = driverId.Value,
                TenantId = tenantId,
                Draft = (int)TripStatus.Draft,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Failed = (int)TripStatus.Failed
            },
            cancellationToken: cancellationToken));

        var linkedBookingIds = new HashSet<int>();
        foreach (var r in tripRows)
        {
            if (r.BookingId is int bid) linkedBookingIds.Add(bid);
            var status = (TripStatus)r.Status;
            var (plat, plng) = (r.PickupLatitude, r.PickupLongitude);
            var (dlat, dlng) = (r.DropLatitude, r.DropLongitude);
            results.Add(new DriverTripDto(
                Id: r.Id,
                BookingNumber: !string.IsNullOrWhiteSpace(r.BookingNumber) ? r.BookingNumber! : r.TripNumber ?? $"T-{r.Id}",
                CustomerName: r.CustomerName ?? "",
                RouteName: r.RouteName ?? "",
                PickupTime: r.PickupTime,
                DropoffTime: r.DropoffTime,
                Status: MapTripToLegacyBookingStatus(status),
                StatusName: DriverTripLabels.Name(status),
                VehicleId: r.VehicleId,
                VehicleName: r.VehicleName,
                TotalAmount: r.TotalAmount,
                PickupAddress: r.PickupAddress,
                PickupLatitude: plat,
                PickupLongitude: plng,
                DropoffAddress: r.DropoffAddress,
                DropLatitude: dlat,
                DropLongitude: dlng,
                GoogleMapsUrl: BuildGoogleMapsUrl(plat, plng, r.PickupAddress, dlat, dlng, r.DropoffAddress),
                GoogleDirectionsUrl: BuildGoogleMapsUrl(plat, plng, r.PickupAddress, dlat, dlng, r.DropoffAddress),
                TripId: r.Id,
                BookingId: r.BookingId,
                Source: "Trip",
                LifecycleStatus: r.Status,
                LifecycleStatusName: DriverTripLabels.Name(status),
                NextActions: DriverTripLabels.NextActions(status)));
        }

        // 2) Legacy bookings without an operational trip
        var bookingRows = await connection.QueryAsync<DriverTripRow>(new CommandDefinition(
            @"SELECT b.Id, b.BookingNumber, c.FullName AS CustomerName,
                     r.Source + ' -> ' + r.Destination AS RouteName,
                     b.PickupTime, b.DropoffTime, b.Status,
                     CASE b.Status
                       WHEN 1 THEN 'Pending' WHEN 2 THEN 'Confirmed' WHEN 3 THEN 'Started'
                       WHEN 4 THEN 'Completed' WHEN 5 THEN 'Cancelled' ELSE 'Unknown' END AS StatusName,
                     b.VehicleId, v.Name AS VehicleName, b.TotalAmount,
                     b.PickupAddress, b.PickupLat AS PickupLatitude, b.PickupLng AS PickupLongitude,
                     b.DropoffAddress, b.DropLat AS DropLatitude, b.DropLng AS DropLongitude,
                     r.Source AS RouteSource, r.Destination AS RouteDestination
              FROM Bookings b
              LEFT JOIN Customers c ON c.Id = b.CustomerId
              LEFT JOIN Routes r ON r.Id = b.RouteId
              LEFT JOIN Vehicles v ON v.Id = b.VehicleId
              WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                AND b.Status IN (@Confirmed, @Started)
              ORDER BY b.PickupTime ASC",
            new
            {
                DriverId = driverId.Value,
                TenantId = tenantId,
                Confirmed = (int)BookingStatus.Confirmed,
                Started = (int)BookingStatus.Started
            },
            cancellationToken: cancellationToken));

        foreach (var r in bookingRows)
        {
            if (linkedBookingIds.Contains(r.Id)) continue;

            var (plat, plng) = ResolveCoords(r.PickupLatitude, r.PickupLongitude, r.RouteSource);
            var (dlat, dlng) = ResolveCoords(r.DropLatitude, r.DropLongitude, r.RouteDestination);
            var pickupAddr = string.IsNullOrWhiteSpace(r.PickupAddress) ? r.RouteSource : r.PickupAddress;
            var dropAddr = string.IsNullOrWhiteSpace(r.DropoffAddress) ? r.RouteDestination : r.DropoffAddress;
            var bookingStatus = (BookingStatus)r.Status;
            var lifecycle = MapBookingToTripStatus(bookingStatus);

            results.Add(new DriverTripDto(
                r.Id, r.BookingNumber, r.CustomerName ?? "", r.RouteName ?? "",
                r.PickupTime, r.DropoffTime, r.Status, r.StatusName ?? "",
                r.VehicleId, r.VehicleName, r.TotalAmount,
                pickupAddr, plat, plng, dropAddr, dlat, dlng,
                BuildGoogleMapsUrl(plat, plng, pickupAddr, dlat, dlng, dropAddr),
                BuildGoogleMapsUrl(plat, plng, pickupAddr, dlat, dlng, dropAddr),
                TripId: null,
                BookingId: r.Id,
                Source: "Booking",
                LifecycleStatus: (int)lifecycle,
                LifecycleStatusName: DriverTripLabels.Name(lifecycle),
                NextActions: DriverTripLabels.NextActionsFromBooking(bookingStatus)));
        }

        return ApiResponse<List<DriverTripDto>>.SuccessResponse(
            results.OrderBy(t => t.PickupTime).ToList());
    }

    private static TripStatus MapBookingToTripStatus(BookingStatus status) => status switch
    {
        BookingStatus.Confirmed => TripStatus.DriverAssigned,
        BookingStatus.Started => TripStatus.Started,
        BookingStatus.Completed => TripStatus.Completed,
        BookingStatus.Cancelled => TripStatus.Cancelled,
        _ => TripStatus.Scheduled
    };

    private static int MapTripToLegacyBookingStatus(TripStatus status) => status switch
    {
        TripStatus.Completed => (int)BookingStatus.Completed,
        TripStatus.Cancelled or TripStatus.Failed => (int)BookingStatus.Cancelled,
        TripStatus.Started or TripStatus.AtPickup or TripStatus.Enroute or TripStatus.Delayed
            => (int)BookingStatus.Started,
        _ => (int)BookingStatus.Confirmed
    };

    private static (double? Lat, double? Lng) ResolveCoords(double? lat, double? lng, string? placeHint)
    {
        if (lat.HasValue && lng.HasValue && !(lat.Value == 0 && lng.Value == 0))
            return (lat, lng);

        if (string.IsNullOrWhiteSpace(placeHint)) return (null, null);
        var source = placeHint.ToLowerInvariant();
        if (source.Contains("lahore")) return (31.5204, 74.3587);
        if (source.Contains("islamabad")) return (33.6844, 73.0479);
        if (source.Contains("sialkot")) return (32.4945, 74.5229);
        if (source.Contains("karachi")) return (24.8607, 67.0011);
        if (source.Contains("multan")) return (30.1575, 71.5249);
        return (null, null);
    }

    private static string? BuildGoogleMapsUrl(
        double? plat, double? plng, string? pAddr,
        double? dlat, double? dlng, string? dAddr)
    {
        static string? Format(double? lat, double? lng, string? address)
        {
            if (lat.HasValue && lng.HasValue)
                return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{lat.Value},{lng.Value}");
            return string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        }

        var origin = Format(plat, plng, pAddr);
        var dest = Format(dlat, dlng, dAddr);
        if (origin is null && dest is null) return null;
        if (dest is null) return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(origin!)}";
        if (origin is null) return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(dest)}";
        return $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(origin)}&destination={Uri.EscapeDataString(dest)}";
    }

    private sealed class OpTripRow
    {
        public int Id { get; init; }
        public string? TripNumber { get; init; }
        public int? BookingId { get; init; }
        public string? BookingNumber { get; init; }
        public string? CustomerName { get; init; }
        public string? RouteName { get; init; }
        public DateTime PickupTime { get; init; }
        public DateTime? DropoffTime { get; init; }
        public int Status { get; init; }
        public int? VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public decimal TotalAmount { get; init; }
        public string? PickupAddress { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public string? DropoffAddress { get; init; }
        public double? DropLatitude { get; init; }
        public double? DropLongitude { get; init; }
    }

    private sealed class DriverTripRow
    {
        public int Id { get; init; }
        public string BookingNumber { get; init; } = "";
        public string? CustomerName { get; init; }
        public string? RouteName { get; init; }
        public DateTime PickupTime { get; init; }
        public DateTime? DropoffTime { get; init; }
        public int Status { get; init; }
        public string? StatusName { get; init; }
        public int? VehicleId { get; init; }
        public string? VehicleName { get; init; }
        public decimal TotalAmount { get; init; }
        public string? PickupAddress { get; init; }
        public double? PickupLatitude { get; init; }
        public double? PickupLongitude { get; init; }
        public string? DropoffAddress { get; init; }
        public double? DropLatitude { get; init; }
        public double? DropLongitude { get; init; }
        public string? RouteSource { get; init; }
        public string? RouteDestination { get; init; }
    }
}

public record GetDriverProfileQuery : IRequest<ApiResponse<DriverProfileDto>>;
public record GetDriverDashboardQuery : IRequest<ApiResponse<DriverDashboardDto>>;
public record GetDriverAttendanceHistoryQuery(DateTime? From, DateTime? To, int Page = 1, int PageSize = 30)
    : IRequest<ApiResponse<List<DriverAttendanceRecordDto>>>;

public record GetDriverTimelineQuery(int Page = 1, int PageSize = 50)
    : IRequest<ApiResponse<List<DriverTimelineEventDto>>>;

public class GetDriverTimelineQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser,
    ITenantContext tenantContext)
    : IRequestHandler<GetDriverTimelineQuery, ApiResponse<List<DriverTimelineEventDto>>>
{
    public async Task<ApiResponse<List<DriverTimelineEventDto>>> Handle(
        GetDriverTimelineQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<List<DriverTimelineEventDto>>.FailResponse("Driver identity required.");

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 50 : request.PageSize;
        var offset = (page - 1) * pageSize;
        var tenantId = tenantContext.GetRequiredTenantId();
        var userId = currentUser.UserId;

        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<DriverTimelineEventDto>(new CommandDefinition(
            """
            WITH Events AS (
                SELECT b.Id AS Id,
                       N'Trip' AS EventType,
                       CONCAT(N'Trip ', b.BookingNumber) AS Title,
                       COALESCE(b.UpdatedAt, b.PickupTime) AS EventTime,
                       CONCAT(
                           ISNULL(c.FullName, N'Customer'),
                           N' · ',
                           CASE b.Status
                             WHEN 1 THEN N'Pending' WHEN 2 THEN N'Confirmed' WHEN 3 THEN N'Started'
                             WHEN 4 THEN N'Completed' WHEN 5 THEN N'Cancelled' ELSE N'Unknown' END
                       ) AS Description,
                       CASE b.Status
                         WHEN 1 THEN N'Pending' WHEN 2 THEN N'Confirmed' WHEN 3 THEN N'Started'
                         WHEN 4 THEN N'Completed' WHEN 5 THEN N'Cancelled' ELSE N'Unknown' END AS Status,
                       b.Id AS ReferenceId
                FROM Bookings b
                LEFT JOIN Customers c ON c.Id = b.CustomerId
                WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0

                UNION ALL

                SELECT f.Id,
                       N'Fuel',
                       N'Fuel receipt',
                       COALESCE(f.FuelDate, f.CreatedAt),
                       CONCAT(CAST(f.Liters AS NVARCHAR(20)), N' L · ', ISNULL(f.Station, N'Station n/a')),
                       N'Submitted',
                       f.Id
                FROM FuelLogs f
                WHERE f.DriverId = @DriverId AND f.IsDeleted = 0

                UNION ALL

                SELECT s.Id,
                       N'Sos',
                       N'SOS alert',
                       s.CreatedAt,
                       CONCAT(
                           ISNULL(s.Message, N'Emergency alert'),
                           CASE WHEN s.Latitude IS NOT NULL AND s.Longitude IS NOT NULL
                                THEN CONCAT(N' · ', FORMAT(s.Latitude, '0.#####'), N',', FORMAT(s.Longitude, '0.#####'))
                                ELSE N'' END
                       ),
                       s.Status,
                       s.Id
                FROM DriverSosAlerts s
                WHERE s.DriverId = @DriverId AND s.TenantId = @TenantId AND s.IsDeleted = 0

                UNION ALL

                SELECT a.Id,
                       N'Attendance',
                       CASE
                         WHEN a.AttendanceType = N'CheckOut' OR a.Status = N'Absent' THEN N'Check-out'
                         WHEN a.AttendanceType = N'CheckIn' OR a.CheckInAt IS NOT NULL THEN N'Check-in'
                         ELSE N'Attendance'
                       END,
                       COALESCE(a.RecordedAt, a.CheckInAt, a.CheckOutAt, a.CreatedAt),
                       a.Notes,
                       COALESCE(a.AttendanceType, a.Status),
                       a.Id
                FROM DriverAttendance a
                WHERE a.DriverId = @DriverId AND a.TenantId = @TenantId AND a.IsDeleted = 0

                UNION ALL

                SELECT n.Id,
                       N'Notification',
                       n.Title,
                       n.CreatedAt,
                       n.Message,
                       CASE WHEN n.IsRead = 1 THEN N'Read' ELSE N'Unread' END,
                       n.Id
                FROM Notifications n
                WHERE n.UserId = @UserId AND n.IsDeleted = 0
            )
            SELECT Id, EventType, Title, EventTime, Description, Status, ReferenceId
            FROM Events
            WHERE EventTime IS NOT NULL
            ORDER BY EventTime DESC
            OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY
            """,
            new
            {
                DriverId = driverId.Value,
                TenantId = tenantId,
                UserId = userId,
                Offset = offset,
                Size = pageSize
            },
            cancellationToken: cancellationToken));

        return ApiResponse<List<DriverTimelineEventDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetDriverEarningsQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<DriverEarningsDto>>;

public class GetDriverEarningsQueryHandler(
    IDbConnectionFactory dbFactory,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDriverEarningsQuery, ApiResponse<DriverEarningsDto>>
{
    public async Task<ApiResponse<DriverEarningsDto>> Handle(GetDriverEarningsQuery request, CancellationToken cancellationToken)
    {
        var driverId = currentUser.DriverId;
        if (!driverId.HasValue)
            return ApiResponse<DriverEarningsDto>.FailResponse("Driver identity required.");

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var from = request.FromDate ?? weekStart.AddDays(-21);
        var to = request.ToDate ?? now;

        using var connection = dbFactory.CreateConnection();
        var d = driverId.Value;
        var completedStatus = (int)BookingStatus.Completed;
        var paidStatus = (int)PaymentStatus.Paid;
        var pendingStatus = (int)PaymentStatus.Pending;
        var partialStatus = (int)PaymentStatus.PartiallyPaid;

        async Task<decimal> SumPayments(DateTime rangeFrom, DateTime rangeTo, int? statusFilter) =>
            await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
                statusFilter is null
                    ? @"SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p
                       INNER JOIN Bookings b ON b.Id = p.BookingId
                       WHERE b.DriverId = @DriverId AND b.IsDeleted = 0 AND p.IsDeleted = 0
                         AND b.PickupTime BETWEEN @From AND @To"
                    : @"SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p
                       INNER JOIN Bookings b ON b.Id = p.BookingId
                       WHERE b.DriverId = @DriverId AND b.IsDeleted = 0 AND p.IsDeleted = 0
                         AND p.Status = @Status AND b.PickupTime BETWEEN @From AND @To",
                new { DriverId = d, From = rangeFrom, To = rangeTo, Status = statusFilter },
                cancellationToken: cancellationToken));

        var todayAmt = await SumPayments(today, now, null);
        var weekAmt = await SumPayments(weekStart, now, null);
        var monthAmt = await SumPayments(monthStart, now, null);
        var rangeAmt = await SumPayments(from, to, null);
        var paidAmt = await SumPayments(from, to, paidStatus);
        var pendingAmt = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p
              INNER JOIN Bookings b ON b.Id = p.BookingId
              WHERE b.DriverId = @DriverId AND b.IsDeleted = 0 AND p.IsDeleted = 0
                AND p.Status IN (@Pending, @Partial) AND b.PickupTime BETWEEN @From AND @To",
            new { DriverId = d, From = from, To = to, Pending = pendingStatus, Partial = partialStatus },
            cancellationToken: cancellationToken));

        var completed = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(*) FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Completed AND IsDeleted = 0
                AND PickupTime BETWEEN @From AND @To",
            new { DriverId = d, Completed = completedStatus, From = from, To = to },
            cancellationToken: cancellationToken));

        var fuelCost = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs
              WHERE DriverId = @DriverId AND IsDeleted = 0
                AND COALESCE(FuelDate, CreatedAt) BETWEEN @From AND @To",
            new { DriverId = d, From = from, To = to },
            cancellationToken: cancellationToken));

        var distanceKm = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(COALESCE(QuotedDistanceKm, 0)), 0) FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Completed AND IsDeleted = 0
                AND PickupTime BETWEEN @From AND @To",
            new { DriverId = d, Completed = completedStatus, From = from, To = to },
            cancellationToken: cancellationToken));

        // Prefer operational Trips distance when present
        var tripDistance = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            @"SELECT SUM(COALESCE(ActualDistanceKm, PlannedDistanceKm, 0))
              FROM Trips
              WHERE DriverId = @DriverId AND IsDeleted = 0 AND Status = @Completed
                AND PlannedStart BETWEEN @From AND @To",
            new { DriverId = d, Completed = (int)TripStatus.Completed, From = from, To = to },
            cancellationToken: cancellationToken));
        if (tripDistance is > 0)
            distanceKm = tripDistance.Value;

        var hours = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(DATEDIFF(MINUTE, PickupTime, COALESCE(DropoffTime, PickupTime))) / 60.0, 0)
              FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Completed AND IsDeleted = 0
                AND PickupTime BETWEEN @From AND @To",
            new { DriverId = d, Completed = completedStatus, From = from, To = to },
            cancellationToken: cancellationToken));

        var dailyRows = await connection.QueryAsync<(DateTime Day, decimal Amount, int TripCount)>(new CommandDefinition(
            @"SELECT CAST(b.PickupTime AS DATE) AS Day,
                     ISNULL(SUM(p.Amount), 0) AS Amount,
                     COUNT(DISTINCT CASE WHEN b.Status = @Completed THEN b.Id END) AS TripCount
              FROM Bookings b
              LEFT JOIN Payments p ON p.BookingId = b.Id AND p.IsDeleted = 0
              WHERE b.DriverId = @DriverId AND b.IsDeleted = 0
                AND b.PickupTime >= @From AND b.PickupTime < @ToExclusive
              GROUP BY CAST(b.PickupTime AS DATE)
              ORDER BY Day",
            new
            {
                DriverId = d,
                Completed = completedStatus,
                From = today.AddDays(-6),
                ToExclusive = today.AddDays(1)
            },
            cancellationToken: cancellationToken));

        var byDay = dailyRows.ToDictionary(r => r.Day.Date, r => r);
        var daily = new List<DriverEarningsDayDto>();
        for (var i = 6; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            if (byDay.TryGetValue(day, out var row))
                daily.Add(new DriverEarningsDayDto(day, row.Amount, row.TripCount));
            else
                daily.Add(new DriverEarningsDayDto(day, 0, 0));
        }

        return ApiResponse<DriverEarningsDto>.SuccessResponse(new DriverEarningsDto(
            rangeAmt,
            completed,
            from,
            to,
            todayAmt,
            weekAmt,
            monthAmt,
            pendingAmt,
            paidAmt,
            fuelCost,
            Math.Round(distanceKm, 1),
            Math.Round(hours, 1),
            daily));
    }
}
