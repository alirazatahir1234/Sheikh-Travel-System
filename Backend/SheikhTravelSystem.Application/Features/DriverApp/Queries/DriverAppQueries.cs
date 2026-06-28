using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
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
                     d.LicenseNumber, d.LicenseExpiryDate, d.Status, d.IsActive,
                     d.Rating, d.YearsExperience, d.VerificationStatus,
                     CASE d.Status
                       WHEN 1 THEN 'Available' WHEN 2 THEN 'On Trip' WHEN 3 THEN 'Off Duty'
                       WHEN 4 THEN 'Suspended' WHEN 5 THEN 'On Leave' ELSE 'Unknown' END AS StatusName,
                     v.Name AS CurrentVehicleName, v.RegistrationNumber AS CurrentVehiclePlate,
                     b.Name AS BranchName
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
                SELECT 1 FROM DriverAttendance WHERE DriverId=@D AND AttendanceType='CheckIn'
                AND CAST(RecordedAt AS DATE)=@Today AND IsDeleted=0
                AND NOT EXISTS(
                    SELECT 1 FROM DriverAttendance da2 WHERE da2.DriverId=@D AND da2.AttendanceType='CheckOut'
                    AND CAST(da2.RecordedAt AS DATE)=@Today AND da2.IsDeleted=0 AND da2.RecordedAt > DriverAttendance.RecordedAt)
            ) THEN 1 ELSE 0 END",
            new { D = driverId.Value, Today = today }, cancellationToken: cancellationToken));

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
            @"SELECT Id, AttendanceType, RecordedAt, Latitude, Longitude, Notes
              FROM DriverAttendance
              WHERE DriverId=@D AND IsDeleted=0 AND RecordedAt BETWEEN @From AND @To
              ORDER BY RecordedAt DESC
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

        var rows = await connection.QueryAsync<DriverTripDto>(new CommandDefinition(
            @"SELECT b.Id, b.BookingNumber, c.FullName AS CustomerName,
                     r.Source + ' -> ' + r.Destination AS RouteName,
                     b.PickupTime, b.DropoffTime, b.Status,
                     CASE b.Status
                       WHEN 1 THEN 'Pending' WHEN 2 THEN 'Confirmed' WHEN 3 THEN 'Started'
                       WHEN 4 THEN 'Completed' WHEN 5 THEN 'Cancelled' ELSE 'Unknown' END AS StatusName,
                     b.VehicleId, v.Name AS VehicleName, b.TotalAmount
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

        return ApiResponse<List<DriverTripDto>>.SuccessResponse(rows.ToList());
    }
}

public record GetDriverProfileQuery : IRequest<ApiResponse<DriverProfileDto>>;
public record GetDriverDashboardQuery : IRequest<ApiResponse<DriverDashboardDto>>;
public record GetDriverAttendanceHistoryQuery(DateTime? From, DateTime? To, int Page = 1, int PageSize = 30)
    : IRequest<ApiResponse<List<DriverAttendanceRecordDto>>>;

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

        var from = request.FromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = request.ToDate ?? DateTime.UtcNow;

        using var connection = dbFactory.CreateConnection();
        var completed = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT COUNT(*) FROM Bookings
              WHERE DriverId = @DriverId AND Status = @Completed AND IsDeleted = 0
                AND PickupTime BETWEEN @From AND @To",
            new { DriverId = driverId.Value, Completed = (int)BookingStatus.Completed, From = from, To = to },
            cancellationToken: cancellationToken));

        var allowances = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            @"SELECT ISNULL(SUM(Amount), 0) FROM Payments p
              INNER JOIN Bookings b ON b.Id = p.BookingId
              WHERE b.DriverId = @DriverId AND b.IsDeleted = 0 AND p.IsDeleted = 0
                AND b.PickupTime BETWEEN @From AND @To",
            new { DriverId = driverId.Value, From = from, To = to },
            cancellationToken: cancellationToken));

        return ApiResponse<DriverEarningsDto>.SuccessResponse(
            new DriverEarningsDto(allowances, completed, from, to));
    }
}
