using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Application.Features.DriverApp.Queries;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class DriverAppRepository(IDbConnectionFactory dbFactory) : IDriverAppRepository
{
    public async Task<DriverLoginRow?> GetDriverLoginByPhoneAsync(string phone, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverLoginRow>(new CommandDefinition(
            @"SELECT d.Id AS DriverId, u.Id AS UserId, d.TenantId, d.FullName, d.Phone, u.PasswordHash
              FROM Drivers d
              INNER JOIN Users u ON u.Id = d.UserId AND u.IsDeleted = 0 AND u.IsActive = 1 AND u.Role = @DriverRole
              WHERE (d.Phone = @Phone OR u.Phone = @Phone)
                AND d.IsDeleted = 0 AND d.IsActive = 1 AND d.TenantId = @TenantId",
            new { Phone = phone.Trim(), TenantId = tenantId, DriverRole = (int)UserRole.Driver },
            cancellationToken: ct));
    }

    public async Task UpdateUserRefreshTokenAsync(int userId, string token, DateTime expiry, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Users SET RefreshToken = @Token, RefreshTokenExpiryTime = @Expiry WHERE Id = @UserId",
            new { Token = token, Expiry = expiry, UserId = userId },
            cancellationToken: ct));
    }

    public async Task<DriverProfileDto?> GetProfileAsync(int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverProfileDto>(new CommandDefinition(
            $@"SELECT d.Id, d.FullName, d.Phone, d.Email, d.PhotoUrl, d.DriverCode,
                     d.LicenseNumber, d.LicenseExpiryDate, d.Status,
                     CASE d.Status
                       WHEN 1 THEN 'Available' WHEN 2 THEN 'On Trip' WHEN 3 THEN 'Off Duty'
                       WHEN 4 THEN 'Suspended' WHEN 5 THEN 'On Leave' ELSE 'Unknown' END AS StatusName,
                     d.IsActive,
                     cv.Name AS CurrentVehicleName, cv.RegistrationNumber AS CurrentVehiclePlate,
                     b.Name AS BranchName,
                     d.Rating, d.YearsExperience, d.VerificationStatus
              FROM Drivers d
              {DriverAppSql.CurrentVehicleApply}
              LEFT JOIN Branches b ON b.Id = d.BranchId
              WHERE d.Id = @DriverId AND d.TenantId = @TenantId AND d.IsDeleted = 0",
            new { DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<DriverDashboardStatsRow?> GetDashboardStatsAsync(
        int driverId, int tenantId, int? userId, DateTime today, DateTime weekStart, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var assigned = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Bookings WHERE DriverId=@D AND TenantId=@T AND Status IN (2,3) AND IsDeleted=0 AND CAST(PickupTime AS DATE)=@Today",
            new { D = driverId, T = tenantId, Today = today }, cancellationToken: ct));

        var completed = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Bookings WHERE DriverId=@D AND TenantId=@T AND Status=4 AND IsDeleted=0 AND CAST(PickupTime AS DATE)=@Today",
            new { D = driverId, T = tenantId, Today = today }, cancellationToken: ct));

        var clockedIn = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM DriverAttendance
                WHERE DriverId=@D AND TenantId=@T AND IsDeleted=0 AND AttendanceDate=@Today
                  AND CheckInAt IS NOT NULL AND CheckOutAt IS NULL
            ) THEN 1 ELSE 0 END",
            new { D = driverId, T = tenantId, Today = today }, cancellationToken: ct));

        var earnings = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(p.Amount),0) FROM Payments p
              INNER JOIN Bookings b ON b.Id=p.BookingId
              WHERE b.DriverId=@D AND b.IsDeleted=0 AND p.IsDeleted=0 AND b.PickupTime>=@WeekStart",
            new { D = driverId, WeekStart = weekStart }, cancellationToken: ct));

        var unread = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Notifications WHERE UserId=@UserId AND IsRead=0 AND IsDeleted=0",
            new { UserId = userId }, cancellationToken: ct));

        var statusRow = await connection.QuerySingleOrDefaultAsync<(string? Vehicle, string? Plate, int Status)>(new CommandDefinition(
            $@"SELECT cv.Name, cv.RegistrationNumber, d.Status FROM Drivers d
              {DriverAppSql.CurrentVehicleApply}
              WHERE d.Id=@DriverId AND d.TenantId=@TenantId AND d.IsDeleted=0",
            new { DriverId = driverId, TenantId = tenantId }, cancellationToken: ct));

        return new DriverDashboardStatsRow
        {
            Assigned = assigned,
            Completed = completed,
            ClockedIn = clockedIn,
            WeekEarnings = earnings,
            Unread = unread,
            Vehicle = statusRow.Vehicle,
            Plate = statusRow.Plate,
            Status = statusRow.Status
        };
    }

    public async Task<int?> GetDriverStatusAsync(int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Status FROM Drivers WHERE Id=@DriverId AND TenantId=@TenantId AND IsDeleted=0",
            new { DriverId = driverId, TenantId = tenantId }, cancellationToken: ct));
    }

    public async Task<int> SetDriverStatusAsync(int driverId, int tenantId, int status, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Drivers
              SET Status = @Status, UpdatedAt = GETUTCDATE()
              WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
            new { Status = status, DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<bool> OwnsBookingAsync(int bookingId, int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM Bookings WHERE Id = @Id AND DriverId = @DriverId AND IsDeleted = 0) THEN 1 ELSE 0 END",
            new { Id = bookingId, DriverId = driverId },
            cancellationToken: ct));
    }

    public async Task<bool> DriverOwnsVehicleAsync(int driverId, int vehicleId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            DriverAppSql.DriverOwnsVehicleExists,
            new { DriverId = driverId, VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<string?> GetVehicleNameAsync(int vehicleId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT Name FROM Vehicles WHERE Id = @Id",
            new { Id = vehicleId },
            cancellationToken: ct));
    }

    public async Task<DriverBookingRef?> ResolveDriverBookingAsync(int id, int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var fromTrip = await connection.QuerySingleOrDefaultAsync<DriverBookingRef>(new CommandDefinition(
            @"SELECT TOP 1 b.Id AS BookingId, b.BookingNumber, b.TotalAmount
              FROM Trips t
              INNER JOIN Bookings b ON b.Id = t.BookingId
              WHERE t.TenantId = @TenantId AND t.DriverId = @DriverId AND t.IsDeleted = 0
                AND b.TenantId = @TenantId AND b.IsDeleted = 0
                AND (t.Id = @Id OR t.BookingId = @Id)",
            new { Id = id, DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
        if (fromTrip is not null) return fromTrip;

        return await connection.QuerySingleOrDefaultAsync<DriverBookingRef>(new CommandDefinition(
            @"SELECT TOP 1 b.Id AS BookingId, b.BookingNumber, b.TotalAmount
              FROM Bookings b
              WHERE b.Id = @Id AND b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0",
            new { Id = id, DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<decimal> GetBookingPaidAmountAsync(int bookingId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(Amount), 0)
              FROM Payments
              WHERE BookingId = @BookingId AND IsDeleted = 0 AND Status IN (@Paid, @Partial)",
            new { BookingId = bookingId, Paid = (int)PaymentStatus.Paid, Partial = (int)PaymentStatus.PartiallyPaid },
            cancellationToken: ct));
    }

    public async Task<(int VehicleId, int? BookingId)?> GetActiveTripVehicleAsync(int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(int VehicleId, int? BookingId)?>(new CommandDefinition(
            @"SELECT TOP 1 VehicleId, BookingId FROM Trips
              WHERE DriverId = @DriverId AND IsDeleted = 0
                AND Status IN (@Started, @AtPickup, @Enroute, @Delayed)
                AND VehicleId IS NOT NULL
              ORDER BY PlannedStart DESC",
            new
            {
                DriverId = driverId,
                Started = (int)TripStatus.Started,
                AtPickup = (int)TripStatus.AtPickup,
                Enroute = (int)TripStatus.Enroute,
                Delayed = (int)TripStatus.Delayed
            },
            cancellationToken: ct));
    }

    public async Task<(int VehicleId, int? BookingId)?> GetActiveBookingVehicleAsync(int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(int VehicleId, int? BookingId)?>(new CommandDefinition(
            @"SELECT TOP 1 VehicleId, Id FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Started AND VehicleId IS NOT NULL AND IsDeleted = 0
              ORDER BY PickupTime DESC",
            new { DriverId = driverId, Started = (int)BookingStatus.Started },
            cancellationToken: ct));
    }

    public async Task<int> UpdateCheckInAsync(int driverId, int tenantId, DateTime attendanceDate, DateTime now, double? lat, double? lng, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE DriverAttendance
              SET Status = N'Present',
                  CheckInAt = COALESCE(CheckInAt, @Now),
                  CheckOutAt = NULL,
                  AttendanceType = N'CheckIn',
                  RecordedAt = @Now,
                  Latitude = COALESCE(@Lat, Latitude),
                  Longitude = COALESCE(@Lng, Longitude)
              WHERE Id = (
                  SELECT TOP 1 Id FROM DriverAttendance
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                    AND AttendanceDate = @AttendanceDate
                  ORDER BY Id DESC
              )",
            new { DriverId = driverId, TenantId = tenantId, AttendanceDate = attendanceDate, Now = now, Lat = lat, Lng = lng },
            cancellationToken: ct));
    }

    public async Task InsertCheckInAsync(int driverId, int tenantId, DateTime attendanceDate, DateTime now, double? lat, double? lng, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO DriverAttendance
                (DriverId, TenantId, AttendanceDate, Status, CheckInAt, AttendanceType, RecordedAt, Latitude, Longitude, IsDeleted, CreatedAt)
              VALUES
                (@DriverId, @TenantId, @AttendanceDate, N'Present', @Now, N'CheckIn', @Now, @Lat, @Lng, 0, @Now)",
            new { DriverId = driverId, TenantId = tenantId, AttendanceDate = attendanceDate, Now = now, Lat = lat, Lng = lng },
            cancellationToken: ct));
    }

    public async Task<int> UpdateCheckOutAsync(int driverId, int tenantId, DateTime attendanceDate, DateTime now, double? lat, double? lng, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE DriverAttendance
              SET CheckOutAt = @Now,
                  AttendanceType = N'CheckOut',
                  RecordedAt = @Now,
                  Latitude = COALESCE(@Lat, Latitude),
                  Longitude = COALESCE(@Lng, Longitude),
                  Status = N'Present'
              WHERE Id = (
                  SELECT TOP 1 Id FROM DriverAttendance
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                    AND AttendanceDate = @AttendanceDate
                    AND CheckOutAt IS NULL
                  ORDER BY COALESCE(CheckInAt, RecordedAt, CreatedAt) DESC
              )",
            new { DriverId = driverId, TenantId = tenantId, AttendanceDate = attendanceDate, Now = now, Lat = lat, Lng = lng },
            cancellationToken: ct));
    }

    public async Task InsertCheckOutAsync(int driverId, int tenantId, DateTime attendanceDate, DateTime now, double? lat, double? lng, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO DriverAttendance
                (DriverId, TenantId, AttendanceDate, Status, CheckOutAt, AttendanceType, RecordedAt, Latitude, Longitude, IsDeleted, CreatedAt)
              VALUES
                (@DriverId, @TenantId, @AttendanceDate, N'Present', @Now, N'CheckOut', @Now, @Lat, @Lng, 0, @Now)",
            new { DriverId = driverId, TenantId = tenantId, AttendanceDate = attendanceDate, Now = now, Lat = lat, Lng = lng },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DriverAttendanceRecordDto>> GetAttendanceHistoryAsync(
        int driverId, DateTime from, DateTime to, int offset, int size, CancellationToken ct = default)
    {
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
            new { D = driverId, From = from, To = to, Offset = offset, Size = size },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(string FullName, string Phone)?> GetDriverNamePhoneAsync(int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var driver = await connection.QuerySingleOrDefaultAsync<(string FullName, string Phone)?>(new CommandDefinition(
            "SELECT FullName, Phone FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
            new { Id = driverId, TenantId = tenantId },
            cancellationToken: ct));
        return driver;
    }

    public async Task<(int? VehicleId, int? BookingId)> GetStartedBookingForSosAsync(int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var active = await connection.QuerySingleOrDefaultAsync<(int? VehicleId, int? BookingId)>(new CommandDefinition(
            @"SELECT TOP 1 VehicleId, Id FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Started AND IsDeleted = 0
              ORDER BY PickupTime DESC",
            new { DriverId = driverId, Started = (int)BookingStatus.Started },
            cancellationToken: ct));
        return active;
    }

    public async Task<int> InsertSosAlertAsync(
        int tenantId, int driverId, int? vehicleId, int? bookingId, double? lat, double? lng, string? message, DateTime createdAt, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO DriverSosAlerts
                (TenantId, DriverId, VehicleId, BookingId, Latitude, Longitude, Message, Status, CreatedAt, IsDeleted)
              VALUES
                (@TenantId, @DriverId, @VehicleId, @BookingId, @Latitude, @Longitude, @Message, N'Open', @CreatedAt, 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                TenantId = tenantId,
                DriverId = driverId,
                VehicleId = vehicleId,
                BookingId = bookingId,
                Latitude = lat,
                Longitude = lng,
                Message = message,
                CreatedAt = createdAt
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DriverOpTripRow>> GetOperationalTripsAsync(int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var tripRows = await connection.QueryAsync<DriverOpTripRow>(new CommandDefinition(
            @"SELECT t.Id, t.TripNumber, t.BookingId, b.BookingNumber,
                     c.FullName AS CustomerName,
                     COALESCE(r.Source + N' -> ' + r.Destination, t.TripName) AS RouteName,
                     t.PlannedStart AS PickupTime, t.PlannedEnd AS DropoffTime,
                     t.Status, t.VehicleId, v.Name AS VehicleName,
                     ISNULL(b.TotalAmount, 0) AS TotalAmount,
                     t.PickupAddress, t.PickupLatitude, t.PickupLongitude,
                     t.DestinationAddress AS DropoffAddress,
                     t.DestinationLatitude AS DropLatitude,
                     t.DestinationLongitude AS DropLongitude,
                     r.Source AS RouteSource, r.Destination AS RouteDestination
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
                DriverId = driverId,
                TenantId = tenantId,
                Draft = (int)TripStatus.Draft,
                Completed = (int)TripStatus.Completed,
                Cancelled = (int)TripStatus.Cancelled,
                Failed = (int)TripStatus.Failed
            },
            cancellationToken: ct));
        return tripRows.ToList();
    }

    public async Task<IReadOnlyList<DriverLegacyTripRow>> GetLegacyBookingTripsAsync(int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var bookingRows = await connection.QueryAsync<DriverLegacyTripRow>(new CommandDefinition(
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
                AND NOT EXISTS (
                    SELECT 1 FROM Trips t2
                    WHERE t2.BookingId = b.Id AND t2.TenantId = @TenantId AND t2.IsDeleted = 0
                )
              ORDER BY b.PickupTime ASC",
            new
            {
                DriverId = driverId,
                TenantId = tenantId,
                Confirmed = (int)BookingStatus.Confirmed,
                Started = (int)BookingStatus.Started
            },
            cancellationToken: ct));
        return bookingRows.ToList();
    }

    public async Task<IReadOnlyList<(int BookingId, decimal PaidAmount)>> GetPaidAmountsForBookingsAsync(
        IReadOnlyList<int> bookingIds, CancellationToken ct = default)
    {
        if (bookingIds.Count == 0) return [];
        using var connection = dbFactory.CreateConnection();
        var paidRows = await connection.QueryAsync<(int BookingId, decimal PaidAmount)>(new CommandDefinition(
            @"SELECT BookingId, ISNULL(SUM(Amount), 0) AS PaidAmount
              FROM Payments
              WHERE BookingId IN @BookingIds
                AND IsDeleted = 0
                AND Status IN (@Paid, @Partial)
              GROUP BY BookingId",
            new
            {
                BookingIds = bookingIds,
                Paid = (int)PaymentStatus.Paid,
                Partial = (int)PaymentStatus.PartiallyPaid
            },
            cancellationToken: ct));
        return paidRows.ToList();
    }

    public async Task<IReadOnlyList<DriverTimelineEventDto>> GetTimelineAsync(
        int driverId, int tenantId, int? userId, int offset, int size, CancellationToken ct = default)
    {
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

                UNION ALL

                SELECT p.Id,
                       N'Payment',
                       N'Payment collected',
                       COALESCE(p.PaymentDate, p.CreatedAt),
                       CONCAT(N'PKR ', FORMAT(p.Amount, 'N0'), N' via ', ISNULL(p.PaymentMethod, N'Unknown')),
                       CAST(p.Status AS NVARCHAR(20)),
                       p.BookingId
                FROM Payments p
                INNER JOIN Bookings b ON b.Id = p.BookingId
                WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId
                  AND b.IsDeleted = 0 AND p.IsDeleted = 0
            )
            SELECT Id, EventType, Title, EventTime, Description, Status, ReferenceId
            FROM Events
            WHERE EventTime IS NOT NULL
            ORDER BY EventTime DESC
            OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY
            """,
            new { DriverId = driverId, TenantId = tenantId, UserId = userId, Offset = offset, Size = size },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<decimal> SumPaymentsAsync(int driverId, DateTime from, DateTime to, int? statusFilter, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            statusFilter is null
                ? @"SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p
                   INNER JOIN Bookings b ON b.Id = p.BookingId
                   WHERE b.DriverId = @DriverId AND b.IsDeleted = 0 AND p.IsDeleted = 0
                     AND b.PickupTime BETWEEN @From AND @To"
                : @"SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p
                   INNER JOIN Bookings b ON b.Id = p.BookingId
                   WHERE b.DriverId = @DriverId AND b.IsDeleted = 0 AND p.IsDeleted = 0
                     AND p.Status = @Status AND b.PickupTime BETWEEN @From AND @To",
            new { DriverId = driverId, From = from, To = to, Status = statusFilter },
            cancellationToken: ct));
    }

    public async Task<decimal> SumPendingPartialPaymentsAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(p.Amount), 0) FROM Payments p
              INNER JOIN Bookings b ON b.Id = p.BookingId
              WHERE b.DriverId = @DriverId AND b.IsDeleted = 0 AND p.IsDeleted = 0
                AND p.Status IN (@Pending, @Partial) AND b.PickupTime BETWEEN @From AND @To",
            new { DriverId = driverId, From = from, To = to, Pending = (int)PaymentStatus.Pending, Partial = (int)PaymentStatus.PartiallyPaid },
            cancellationToken: ct));
    }

    public async Task<int> CountCompletedBookingsAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(*) FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Completed AND IsDeleted = 0
                AND PickupTime BETWEEN @From AND @To",
            new { DriverId = driverId, Completed = (int)BookingStatus.Completed, From = from, To = to },
            cancellationToken: ct));
    }

    public async Task<decimal> SumFuelCostAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs
              WHERE DriverId = @DriverId AND IsDeleted = 0
                AND COALESCE(FuelDate, CreatedAt) BETWEEN @From AND @To",
            new { DriverId = driverId, From = from, To = to },
            cancellationToken: ct));
    }

    public async Task<decimal> SumBookingDistanceAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(COALESCE(QuotedDistanceKm, 0)), 0) FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Completed AND IsDeleted = 0
                AND PickupTime BETWEEN @From AND @To",
            new { DriverId = driverId, Completed = (int)BookingStatus.Completed, From = from, To = to },
            cancellationToken: ct));
    }

    public async Task<decimal?> SumTripDistanceAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            @"SELECT SUM(COALESCE(ActualDistanceKm, PlannedDistanceKm, 0))
              FROM Trips
              WHERE DriverId = @DriverId AND IsDeleted = 0 AND Status = @Completed
                AND PlannedStart BETWEEN @From AND @To",
            new { DriverId = driverId, Completed = (int)TripStatus.Completed, From = from, To = to },
            cancellationToken: ct));
    }

    public async Task<decimal> SumBookingHoursAsync(int driverId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(DATEDIFF(MINUTE, PickupTime, COALESCE(DropoffTime, PickupTime))) / 60.0, 0)
              FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Completed AND IsDeleted = 0
                AND PickupTime BETWEEN @From AND @To",
            new { DriverId = driverId, Completed = (int)BookingStatus.Completed, From = from, To = to },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<(DateTime Day, decimal Amount, int TripCount)>> GetDailyEarningsAsync(
        int driverId, DateTime from, DateTime toExclusive, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
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
                DriverId = driverId,
                Completed = (int)BookingStatus.Completed,
                From = from,
                ToExclusive = toExclusive
            },
            cancellationToken: ct));
        return dailyRows.ToList();
    }

    public async Task<IReadOnlyList<DriverFuelReceiptRow>> GetFuelReceiptsAsync(int driverId, int offset, int size, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<DriverFuelReceiptRow>(new CommandDefinition(
            """
            SELECT f.Id, f.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehiclePlate,
                   f.Liters, f.PricePerLiter, f.TotalCost, f.OdometerReading, f.FuelType,
                   f.FuelDate, f.Station, f.ReceiptUrl
            FROM FuelLogs f
            LEFT JOIN Vehicles v ON v.Id = f.VehicleId
            WHERE f.DriverId = @DriverId AND f.IsDeleted = 0
            ORDER BY f.FuelDate DESC
            OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY
            """,
            new { DriverId = driverId, Offset = offset, Size = size },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(int Id, string Name, string? Description, string ChecklistJson)?> GetInspectionTemplateAsync(int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(int Id, string Name, string? Description, string ChecklistJson)?>(
            new CommandDefinition(
                @"SELECT TOP 1 Id, Name, Description, ChecklistJson
                  FROM InspectionTemplates
                  WHERE IsDeleted = 0 AND IsActive = 1
                    AND (TenantId IS NULL OR TenantId = @TenantId)
                  ORDER BY CASE WHEN Name LIKE N'%Standard%' THEN 0 ELSE 1 END, Id",
                new { TenantId = tenantId },
                cancellationToken: ct));
        return row;
    }

    public async Task<(int Id, string ChecklistJson)?> GetDefaultInspectionTemplateAsync(int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(int Id, string ChecklistJson)?>(new CommandDefinition(
            @"SELECT TOP 1 Id, ChecklistJson FROM InspectionTemplates
              WHERE IsDeleted = 0 AND IsActive = 1
                AND (TenantId IS NULL OR TenantId = @TenantId)
              ORDER BY CASE WHEN Name LIKE N'%Standard%' THEN 0 ELSE 1 END, Id",
            new { TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<string?> GetInspectionChecklistJsonAsync(int templateId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            @"SELECT ChecklistJson FROM InspectionTemplates
              WHERE Id = @Id AND IsDeleted = 0 AND IsActive = 1",
            new { Id = templateId },
            cancellationToken: ct));
    }

    public async Task<bool> VehicleExistsForTenantAsync(int vehicleId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0
              ) THEN 1 ELSE 0 END",
            new { Id = vehicleId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<string?> GetDriverFullNameAsync(int driverId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT FullName FROM Drivers WHERE Id = @Id",
            new { Id = driverId },
            cancellationToken: ct));
    }

    public async Task<int> InsertInspectionAsync(DriverInspectionInsert insert, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Inspections
                (TenantId, VehicleId, TemplateId, DriverId, InspectedBy, InspectionDate,
                 OdometerReading, Result, ResultsJson, PhotosJson, Comments, CreatedAt, CreatedBy, IsDeleted)
            VALUES
                (@TenantId, @VehicleId, @TemplateId, @DriverId, @InspectedBy, GETUTCDATE(),
                 @Odometer, @Result, @ResultsJson, N'[]', @Comments, GETUTCDATE(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """,
            new
            {
                insert.TenantId,
                insert.VehicleId,
                insert.TemplateId,
                insert.DriverId,
                insert.InspectedBy,
                insert.Odometer,
                insert.Result,
                insert.ResultsJson,
                insert.Comments,
                insert.CreatedBy
            },
            cancellationToken: ct));
    }

    public async Task UpdateInspectionMediaAsync(int id, string photosJson, string? signatureUrl, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Inspections SET PhotosJson = @Photos, SignatureUrl = @Signature WHERE Id = @Id",
            new { Id = id, Photos = photosJson, Signature = signatureUrl },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DriverInspectionHistoryRow>> GetInspectionHistoryAsync(
        int driverId, int tenantId, int offset, int size, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<DriverInspectionHistoryRow>(new CommandDefinition(
            @"SELECT i.Id, i.VehicleId, v.Name AS VehicleName, v.RegistrationNumber AS VehiclePlate,
                     i.InspectionDate, i.Result, i.OdometerReading, i.Comments,
                     i.PhotosJson, i.SignatureUrl
              FROM Inspections i
              LEFT JOIN Vehicles v ON v.Id = i.VehicleId
              WHERE i.DriverId = @DriverId AND i.IsDeleted = 0
                AND (i.TenantId IS NULL OR i.TenantId = @TenantId)
              ORDER BY i.InspectionDate DESC
              OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY",
            new { DriverId = driverId, TenantId = tenantId, Offset = offset, Size = size },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DriverInspectionVehicleDto>> GetVehiclesForInspectionAsync(
        int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<DriverInspectionVehicleDto>(new CommandDefinition(
            $"""
            SELECT DISTINCT v.Id, v.Name, v.RegistrationNumber AS Plate FROM (
                {DriverAppSql.AssignedVehicleIdsUnion}
                UNION
                SELECT TOP 1 VehicleId FROM Bookings
                WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0 AND VehicleId IS NOT NULL
                ORDER BY PickupTime DESC
            ) x
            INNER JOIN Vehicles v ON v.Id = x.VehicleId AND v.IsDeleted = 0
            """,
            new { DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<DriverInspectionVehicleDto>> GetFallbackVehiclesForInspectionAsync(
        int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var list = await connection.QueryAsync<DriverInspectionVehicleDto>(new CommandDefinition(
            @"SELECT TOP 20 Id, Name, RegistrationNumber AS Plate
              FROM Vehicles WHERE TenantId = @TenantId AND IsDeleted = 0 AND Status IN (1, 2)
              ORDER BY Name",
            new { TenantId = tenantId },
            cancellationToken: ct));
        return list.ToList();
    }

    public async Task<int?> UpsertDriverDeviceAsync(DriverDeviceUpsert upsert, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
            MERGE DriverDevices AS t
            USING (SELECT @DriverId AS DriverId, @DeviceId AS DeviceId) AS s
            ON t.DriverId = s.DriverId AND t.DeviceId = s.DeviceId AND t.IsDeleted = 0
            WHEN MATCHED THEN UPDATE SET
                Platform = @Platform,
                Model = @Model,
                OsVersion = @OsVersion,
                AppVersion = @AppVersion,
                PackageName = @PackageName,
                InstallerStore = @InstallerStore,
                FingerprintHash = @FingerprintHash,
                IsEmulator = @IsEmulator,
                IsRooted = @IsRooted,
                IsJailbroken = @IsJailbroken,
                IsTampered = @IsTampered,
                PinningConfigured = @PinningConfigured,
                UserId = @UserId,
                LastSeenAt = @Now,
                UpdatedAt = @Now
            WHEN NOT MATCHED THEN INSERT
                (TenantId, DriverId, UserId, DeviceId, Platform, Model, OsVersion, AppVersion,
                 PackageName, InstallerStore, FingerprintHash, IsEmulator, IsRooted, IsJailbroken,
                 IsTampered, PinningConfigured, LastSeenAt, CreatedAt, IsDeleted)
            VALUES
                (@TenantId, @DriverId, @UserId, @DeviceId, @Platform, @Model, @OsVersion, @AppVersion,
                 @PackageName, @InstallerStore, @FingerprintHash, @IsEmulator, @IsRooted, @IsJailbroken,
                 @IsTampered, @PinningConfigured, @Now, @Now, 0);

            SELECT Id FROM DriverDevices
            WHERE DriverId = @DriverId AND DeviceId = @DeviceId AND IsDeleted = 0;
            """,
            upsert,
            cancellationToken: ct));
    }

    public async Task<(string? Cnic, DateTime? LicenseExpiry)?> GetDriverComplianceProfileAsync(
        int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(string? Cnic, DateTime? LicenseExpiry)?>(
            new CommandDefinition(
                "SELECT CNIC, LicenseExpiryDate FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = driverId, TenantId = tenantId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DriverComplianceDocRow>> GetDriverComplianceDocsAsync(
        int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var driverDocs = await connection.QueryAsync<DriverComplianceDocRow>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, FileUrl, ExpiryDate, Status
                  FROM ComplianceDocuments
                  WHERE EntityType = N'Driver' AND EntityId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { DriverId = driverId, TenantId = tenantId },
                cancellationToken: ct));
        return driverDocs.ToList();
    }

    public async Task<(int Id, string Name)?> GetPrimaryAssignedVehicleAsync(int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<(int Id, string Name)?>(new CommandDefinition(
            $@"SELECT TOP 1 v.Id, v.Name FROM (
                {DriverAppSql.AssignedVehicleIdsUnion}
              ) x
              INNER JOIN Vehicles v ON v.Id = x.VehicleId AND v.IsDeleted = 0
              ORDER BY CASE WHEN EXISTS(
                SELECT 1 FROM AssignmentHistory ah
                WHERE ah.DriverId = @DriverId AND ah.VehicleId = x.VehicleId
                  AND ah.TenantId = @TenantId AND ah.IsDeleted = 0 AND ah.Status = N'Active'
              ) THEN 0 ELSE 1 END",
            new { DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<VehicleComplianceDocRow>> GetVehicleComplianceDocsAsync(
        int vehicleId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var vDocs = await connection.QueryAsync<VehicleComplianceDocRow>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, FileUrl, ExpiryDate FROM (
                    SELECT Id, DocumentType, FileUrl, ExpiryDate, CreatedAt,
                      ROW_NUMBER() OVER (PARTITION BY DocumentType ORDER BY CreatedAt DESC) AS rn
                    FROM VehicleDocuments
                    WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
                      AND DocumentType IN (N'Registration', N'Insurance', N'Permit')
                  ) r WHERE rn = 1",
                new { VehicleId = vehicleId, TenantId = tenantId },
                cancellationToken: ct));
        return vDocs.ToList();
    }

    public async Task<DriverTripRef?> FindDriverTripAsync(int id, int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverTripRef>(new CommandDefinition(
            @"SELECT TOP 1 Id, Status, BookingId, VehicleId
              FROM Trips
              WHERE TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
                AND (Id = @Id OR BookingId = @Id)
              ORDER BY CASE WHEN Id = @Id THEN 0 ELSE 1 END, Id DESC",
            new { Id = id, DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<bool> OwnsBookingForTenantAsync(int bookingId, int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM Bookings
                WHERE Id = @Id AND DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
              ) THEN 1 ELSE 0 END",
            new { Id = bookingId, DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<DriverTripRef?> FindTripByBookingAsync(int bookingId, int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverTripRef>(new CommandDefinition(
            @"SELECT TOP 1 Id, Status, BookingId, VehicleId
              FROM Trips
              WHERE BookingId = @BookingId AND TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0
              ORDER BY Id DESC",
            new { BookingId = bookingId, TenantId = tenantId, DriverId = driverId },
            cancellationToken: ct));
    }

    public async Task<DriverTripRef?> GetTripRefAsync(int tripId, int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DriverTripRef>(new CommandDefinition(
            @"SELECT Id, Status, BookingId, VehicleId
              FROM Trips
              WHERE Id = @Id AND TenantId = @TenantId AND DriverId = @DriverId AND IsDeleted = 0",
            new { Id = tripId, TenantId = tenantId, DriverId = driverId },
            cancellationToken: ct));
    }

    public async Task EnsureTripVehicleAsync(int tripId, int? bookingId, int driverId, int tenantId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        var vehicleId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            @"SELECT TOP 1 COALESCE(t.VehicleId, ah.VehicleId)
              FROM Trips t
              OUTER APPLY (
                  SELECT TOP 1 VehicleId FROM AssignmentHistory
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Active'
                  ORDER BY StartAt DESC
              ) ah
              WHERE t.Id = @TripId AND t.TenantId = @TenantId",
            new { TripId = tripId, DriverId = driverId, TenantId = tenantId },
            cancellationToken: ct));

        if (!vehicleId.HasValue) return;

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Trips SET VehicleId = @VehicleId, UpdatedAt = GETUTCDATE()
              WHERE Id = @TripId AND TenantId = @TenantId AND (VehicleId IS NULL OR VehicleId = 0);
              UPDATE Bookings SET VehicleId = @VehicleId, UpdatedAt = GETUTCDATE()
              WHERE Id = @BookingId AND TenantId = @TenantId AND (VehicleId IS NULL OR VehicleId = 0);",
            new { VehicleId = vehicleId.Value, TripId = tripId, BookingId = bookingId, TenantId = tenantId },
            cancellationToken: ct));
    }

    public async Task<int?> GetBookingStatusAsync(int bookingId, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Status FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
            new { Id = bookingId },
            cancellationToken: ct));
    }

    public async Task SyncLinkedBookingStatusAsync(int bookingId, int status, int cancelledStatus, string? reason, CancellationToken ct = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Bookings SET Status = @Status, UpdatedAt = GETUTCDATE(),
                CancellationReason = CASE WHEN @Status = @Cancelled THEN @Reason ELSE CancellationReason END
              WHERE Id = @Id AND IsDeleted = 0",
            new { Id = bookingId, Status = status, Cancelled = cancelledStatus, Reason = reason },
            cancellationToken: ct));
    }
}
