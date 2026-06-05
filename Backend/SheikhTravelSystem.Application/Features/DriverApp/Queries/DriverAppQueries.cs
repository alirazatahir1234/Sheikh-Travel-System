using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverApp.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverApp.Queries;

public record GetDriverTripsQuery : IRequest<ApiResponse<List<DriverTripDto>>>;

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
