using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverPerformanceSummaryQuery(int DriverId, DateTime? FromDate = null, DateTime? ToDate = null)
    : IRequest<ApiResponse<DriverPerformanceSummaryDto>>;

public class GetDriverPerformanceSummaryQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverPerformanceSummaryQuery, ApiResponse<DriverPerformanceSummaryDto>>
{
    public async Task<ApiResponse<DriverPerformanceSummaryDto>> Handle(
        GetDriverPerformanceSummaryQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-3);
        var to = request.ToDate ?? DateTime.UtcNow;

        var driver = await connection.QuerySingleOrDefaultAsync<(string FullName, decimal? Rating, int? YearsExperience)>(
            new CommandDefinition(
                "SELECT FullName, Rating, YearsExperience FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (driver.FullName is null)
            return ApiResponse<DriverPerformanceSummaryDto>.FailResponse("Driver not found.");

        var trips = await connection.QuerySingleAsync<(int Total, int Completed, decimal Revenue)>(
            new CommandDefinition(
                @"SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END) AS Completed,
                    ISNULL(SUM(CASE WHEN Status = @Completed THEN TotalAmount ELSE 0 END), 0) AS Revenue
                  FROM Bookings
                  WHERE DriverId = @DriverId AND IsDeleted = 0
                    AND PickupTime >= @From AND PickupTime <= @To",
                new { request.DriverId, From = from, To = to, Completed = (int)Domain.Enums.BookingStatus.Completed },
                cancellationToken: cancellationToken));

        var violations = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM DriverViolations WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var attendancePresent = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"SELECT COUNT(*) FROM DriverAttendance
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0 AND Status = N'Present'",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var completionRate = trips.Total > 0 ? Math.Round(trips.Completed * 100m / trips.Total, 1) : 0;

        return ApiResponse<DriverPerformanceSummaryDto>.SuccessResponse(new DriverPerformanceSummaryDto(
            request.DriverId,
            driver.FullName,
            driver.Rating,
            driver.YearsExperience,
            trips.Total,
            trips.Completed,
            trips.Revenue,
            completionRate,
            violations,
            attendancePresent));
    }
}

public record GetDriverViolationsQuery(int DriverId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<DriverViolationDto>>>;

public class GetDriverViolationsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverViolationsQuery, ApiResponse<PagedResult<DriverViolationDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverViolationDto>>> Handle(
        GetDriverViolationsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var offset = (request.Page - 1) * request.PageSize;

        var items = (await connection.QueryAsync<DriverViolationDto>(
            new CommandDefinition(
                @"SELECT Id, ViolationType, Severity, OccurredAt, Description, BookingId, Status, CreatedAt
                  FROM DriverViolations
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                  ORDER BY OccurredAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { request.DriverId, TenantId = tenantId, Offset = offset, request.PageSize },
                cancellationToken: cancellationToken))).ToList();

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM DriverViolations WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<DriverViolationDto>>.SuccessResponse(new PagedResult<DriverViolationDto>
        {
            Items = items, TotalCount = total, Page = request.Page, PageSize = request.PageSize
        });
    }
}

public record GetDriverAttendanceQuery(int DriverId, DateTime? FromDate = null, DateTime? ToDate = null)
    : IRequest<ApiResponse<List<DriverAttendanceDto>>>;

public class GetDriverAttendanceQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverAttendanceQuery, ApiResponse<List<DriverAttendanceDto>>>
{
    public async Task<ApiResponse<List<DriverAttendanceDto>>> Handle(
        GetDriverAttendanceQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var from = (request.FromDate ?? DateTime.UtcNow.AddDays(-30)).Date;
        var to = (request.ToDate ?? DateTime.UtcNow).Date;

        var items = (await connection.QueryAsync<DriverAttendanceDto>(
            new CommandDefinition(
                @"SELECT Id, AttendanceDate, Status, CheckInAt, CheckOutAt, Notes, CreatedAt
                  FROM DriverAttendance
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                    AND AttendanceDate >= @From AND AttendanceDate <= @To
                  ORDER BY AttendanceDate DESC",
                new { request.DriverId, TenantId = tenantId, From = from, To = to },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<List<DriverAttendanceDto>>.SuccessResponse(items);
    }
}

public record GetDriverLocationQuery(int DriverId) : IRequest<ApiResponse<DriverLocationDto>>;

public class GetDriverLocationQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverLocationQuery, ApiResponse<DriverLocationDto>>
{
    public async Task<ApiResponse<DriverLocationDto>> Handle(GetDriverLocationQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var row = await connection.QuerySingleOrDefaultAsync<DriverLocationDto>(
            new CommandDefinition(
                @"SELECT TOP 1
                    gp.Latitude, gp.Longitude, gp.Speed, gp.Ignition, gp.Timestamp AS LastSeen,
                    v.Id AS VehicleId, v.RegistrationNumber AS VehicleRegistration,
                    CAST(CASE WHEN gd.LastSeenAt >= DATEADD(minute, -15, GETUTCDATE()) THEN 1 ELSE 0 END AS BIT) AS GpsOnline
                  FROM AssignmentHistory ah
                  INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
                  LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
                  OUTER APPLY (
                    SELECT TOP 1 p.Latitude, p.Longitude, p.Speed, p.Ignition, p.Timestamp
                    FROM GpsPositions p
                    WHERE (p.DriverId = @DriverId OR p.VehicleId = v.Id)
                    ORDER BY p.Timestamp DESC
                  ) gp
                  WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId
                    AND ah.Status = N'Active' AND ah.IsDeleted = 0
                  ORDER BY ah.StartAt DESC",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (row is null)
            return ApiResponse<DriverLocationDto>.FailResponse("No active vehicle assignment for GPS tracking.");

        return ApiResponse<DriverLocationDto>.SuccessResponse(row);
    }
}

public record GetDriverLocationHistoryQuery(int DriverId, DateTime From, DateTime To)
    : IRequest<ApiResponse<List<DriverLocationPointDto>>>;

public class GetDriverLocationHistoryQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverLocationHistoryQuery, ApiResponse<List<DriverLocationPointDto>>>
{
    public async Task<ApiResponse<List<DriverLocationPointDto>>> Handle(
        GetDriverLocationHistoryQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicleId = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                @"SELECT TOP 1 VehicleId FROM AssignmentHistory
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0
                  ORDER BY CASE WHEN Status = N'Active' THEN 0 ELSE 1 END, StartAt DESC",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!vehicleId.HasValue)
            return ApiResponse<List<DriverLocationPointDto>>.SuccessResponse([]);

        var points = (await connection.QueryAsync<DriverLocationPointDto>(
            new CommandDefinition(
                @"SELECT Latitude, Longitude, Speed, Timestamp
                  FROM GpsPositions
                  WHERE (DriverId = @DriverId OR VehicleId = @VehicleId)
                    AND Timestamp >= @From AND Timestamp <= @To
                  ORDER BY Timestamp ASC",
                new { request.DriverId, VehicleId = vehicleId.Value, request.From, request.To },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<List<DriverLocationPointDto>>.SuccessResponse(points);
    }
}
