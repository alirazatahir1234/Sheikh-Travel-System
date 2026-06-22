using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverTimelineQuery(int DriverId) : IRequest<ApiResponse<IReadOnlyList<DriverTimelineEventDto>>>;

public class GetDriverTimelineQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverTimelineQuery, ApiResponse<IReadOnlyList<DriverTimelineEventDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverTimelineEventDto>>> Handle(
        GetDriverTimelineQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var events = (await connection.QueryAsync<DriverTimelineEventDto>(
            new CommandDefinition(
                @"SELECT * FROM (
                    SELECT d.Id AS Id, N'Registered' AS EventType, N'Driver registered' AS Title,
                      CONCAT(N'Code: ', ISNULL(d.DriverCode, N'—')) AS Description, d.CreatedAt AS OccurredAt
                    FROM Drivers d
                    WHERE d.Id = @DriverId AND d.TenantId = @TenantId AND d.IsDeleted = 0
                    UNION ALL
                    SELECT c.Id, N'DocumentUploaded', CONCAT(N'Document uploaded: ', c.DocumentType),
                      NULL, c.CreatedAt
                    FROM ComplianceDocuments c
                    WHERE c.EntityType = N'Driver' AND c.EntityId = @DriverId AND c.TenantId = @TenantId AND c.IsDeleted = 0
                    UNION ALL
                    SELECT a.Id, N'AssignedVehicle', N'Vehicle assigned',
                      CONCAT(N'Assignment #', a.Id), a.StartAt
                    FROM AssignmentHistory a
                    WHERE a.DriverId = @DriverId AND a.TenantId = @TenantId AND a.IsDeleted = 0
                  ) timeline
                  ORDER BY OccurredAt DESC",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        return ApiResponse<IReadOnlyList<DriverTimelineEventDto>>.SuccessResponse(events);
    }
}

public record GetDriverActiveDutyQuery(int DriverId) : IRequest<ApiResponse<DriverActiveDutyDto>>;

public record DriverActiveDutyDto(
    IReadOnlyList<DriverTripSummaryDto> RecentTrips,
    int FuelLogCount,
    bool HasGpsAssignment);

public record DriverTripSummaryDto(int Id, string Status, DateTime? TripDate, string? Route);

public class GetDriverActiveDutyQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverActiveDutyQuery, ApiResponse<DriverActiveDutyDto>>
{
    public async Task<ApiResponse<DriverActiveDutyDto>> Handle(
        GetDriverActiveDutyQuery request,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var trips = (await connection.QueryAsync<DriverTripSummaryDto>(
            new CommandDefinition(
                @"SELECT TOP 5 b.Id, CAST(b.Status AS NVARCHAR(20)) AS Status, b.PickupTime AS TripDate, r.Name AS Route
                  FROM Bookings b
                  LEFT JOIN Routes r ON r.Id = b.RouteId
                  WHERE b.DriverId = @DriverId AND b.TenantId = @TenantId AND b.IsDeleted = 0
                  ORDER BY b.PickupTime DESC",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        var fuelCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM FuelLogs WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var hasGps = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM AssignmentHistory ah
                    INNER JOIN Vehicles v ON v.Id = ah.VehicleId
                    WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId
                      AND ah.Status = N'Active' AND ah.IsDeleted = 0 AND v.GpsDeviceId IS NOT NULL
                  ) THEN 1 ELSE 0 END",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<DriverActiveDutyDto>.SuccessResponse(
            new DriverActiveDutyDto(trips, fuelCount, hasGps));
    }
}

public record GetDriverAssignmentsQuery(int DriverId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<DriverAssignmentDto>>>;

public class GetDriverAssignmentsQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverAssignmentsQuery, ApiResponse<PagedResult<DriverAssignmentDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverAssignmentDto>>> Handle(
        GetDriverAssignmentsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var offset = (request.Page - 1) * request.PageSize;

        var items = (await connection.QueryAsync<DriverAssignmentDto>(
            new CommandDefinition(
                @"SELECT ah.Id, ah.VehicleId, v.RegistrationNumber AS VehicleRegistration, v.VehicleCode,
                         v.Name AS VehicleName, v.Make AS VehicleMake, v.Model AS VehicleModel, v.Color AS VehicleColor,
                         ah.AssignmentType, ah.Status, ah.StartAt, ah.EndAt, ah.BookingId,
                         ah.CreatedBy AS AssignedBy, ah.Notes AS Remarks
                  FROM AssignmentHistory ah
                  INNER JOIN Vehicles v ON v.Id = ah.VehicleId AND v.IsDeleted = 0
                  WHERE ah.DriverId = @DriverId AND ah.TenantId = @TenantId AND ah.IsDeleted = 0
                  ORDER BY ah.StartAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { request.DriverId, TenantId = tenantId, Offset = offset, request.PageSize },
                cancellationToken: cancellationToken))).ToList();

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM AssignmentHistory WHERE DriverId = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<DriverAssignmentDto>>.SuccessResponse(new PagedResult<DriverAssignmentDto>
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}

public record GetDriversAvailabilityQuery(int? BranchId = null)
    : IRequest<ApiResponse<DriversAvailabilitySummaryDto>>;

public class GetDriversAvailabilityQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriversAvailabilityQuery, ApiResponse<DriversAvailabilitySummaryDto>>
{
    public async Task<ApiResponse<DriversAvailabilitySummaryDto>> Handle(
        GetDriversAvailabilityQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();
        var bucketSql = DriverAvailabilityHelper.BucketSqlExpression;

        var where = "d.IsDeleted = 0 AND d.TenantId = @TenantId";
        if (request.BranchId.HasValue)
            where += " AND d.BranchId = @BranchId";

        var row = await connection.QuerySingleAsync<DriversAvailabilitySummaryDto>(
            new CommandDefinition(
                $@"SELECT
                    SUM(CASE WHEN bucket = N'Available' THEN 1 ELSE 0 END) AS Available,
                    SUM(CASE WHEN bucket = N'Busy' THEN 1 ELSE 0 END) AS Busy,
                    SUM(CASE WHEN bucket = N'OnTrip' THEN 1 ELSE 0 END) AS OnTrip,
                    SUM(CASE WHEN bucket = N'Unavailable' THEN 1 ELSE 0 END) AS Unavailable
                  FROM (
                    SELECT {bucketSql} AS bucket
                    FROM Drivers d
                    WHERE {where}
                  ) x",
                new
                {
                    TenantId = tenantId,
                    request.BranchId,
                    OnTrip = (int)DriverStatus.OnTrip,
                    OffDuty = (int)DriverStatus.OffDuty,
                    Available = (int)DriverStatus.Available,
                    OnLeave = (int)DriverStatus.OnLeave,
                    Suspended = (int)DriverStatus.Suspended
                },
                cancellationToken: cancellationToken));

        return ApiResponse<DriversAvailabilitySummaryDto>.SuccessResponse(row);
    }
}

public record GetDriverAvailabilityDetailQuery(int DriverId)
    : IRequest<ApiResponse<DriverAvailabilityDetailDto>>;

public class GetDriverAvailabilityDetailQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetDriverAvailabilityDetailQuery, ApiResponse<DriverAvailabilityDetailDto>>
{
    public async Task<ApiResponse<DriverAvailabilityDetailDto>> Handle(
        GetDriverAvailabilityDetailQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            return ApiResponse<DriverAvailabilityDetailDto>.FailResponse("Driver not found.");

        var row = await connection.QuerySingleAsync<(bool IsActive, int Status, bool LicenseExpired, bool HasAssignment)>(
            new CommandDefinition(
                @"SELECT d.IsActive, d.Status,
                         CASE WHEN d.LicenseExpiryDate < CAST(GETUTCDATE() AS DATE) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END,
                         CASE WHEN EXISTS (
                            SELECT 1 FROM AssignmentHistory ah
                            WHERE ah.DriverId = d.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                         ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
                  FROM Drivers d
                  WHERE d.Id = @Id AND d.TenantId = @TenantId AND d.IsDeleted = 0",
                new { Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var bucket = DriverAvailabilityHelper.Compute(
            row.IsActive,
            (DriverStatus)row.Status,
            row.HasAssignment,
            row.LicenseExpired);

        return ApiResponse<DriverAvailabilityDetailDto>.SuccessResponse(
            new DriverAvailabilityDetailDto(request.DriverId, bucket.ToString(), (DriverStatus)row.Status, row.HasAssignment));
    }
}
