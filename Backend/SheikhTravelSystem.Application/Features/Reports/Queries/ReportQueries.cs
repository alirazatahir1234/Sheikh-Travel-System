using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Reports.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Reports.Queries;

public record GetBookingReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<BookingReportDto>>;

public class GetBookingReportQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetBookingReportQuery, ApiResponse<BookingReportDto>>
{
    public async Task<ApiResponse<BookingReportDto>> Handle(GetBookingReportQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = request.ToDate ?? DateTime.UtcNow;

        var report = await connection.QuerySingleAsync<BookingReportDto>(
            @"SELECT
                COUNT(*) AS TotalBookings,
                SUM(CASE WHEN Status = @Completed THEN 1 ELSE 0 END) AS Completed,
                SUM(CASE WHEN Status = @Cancelled THEN 1 ELSE 0 END) AS Cancelled,
                SUM(CASE WHEN Status = @Pending THEN 1 ELSE 0 END) AS Pending,
                SUM(CASE WHEN Status = @Started THEN 1 ELSE 0 END) AS Active
              FROM Bookings
              WHERE CreatedAt BETWEEN @From AND @To AND IsDeleted = 0",
            new
            {
                Completed = (int)BookingStatus.Completed,
                Cancelled = (int)BookingStatus.Cancelled,
                Pending = (int)BookingStatus.Pending,
                Started = (int)BookingStatus.Started,
                From = from, To = to
            });

        return ApiResponse<BookingReportDto>.SuccessResponse(report);
    }
}

public record GetRevenueReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<RevenueReportDto>>;

public class GetRevenueReportQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetRevenueReportQuery, ApiResponse<RevenueReportDto>>
{
    public async Task<ApiResponse<RevenueReportDto>> Handle(GetRevenueReportQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = request.ToDate ?? DateTime.UtcNow;

        var totalRevenue = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(Amount), 0) FROM Payments WHERE Status = @Paid AND PaymentDate BETWEEN @From AND @To AND IsDeleted = 0",
            new { Paid = (int)PaymentStatus.Paid, From = from, To = to });

        var fuelExpense = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(TotalCost), 0) FROM FuelLogs WHERE FuelDate BETWEEN @From AND @To AND IsDeleted = 0",
            new { From = from, To = to });

        var maintenanceCost = await connection.ExecuteScalarAsync<decimal>(
            "SELECT ISNULL(SUM(Cost), 0) FROM Maintenance WHERE MaintenanceDate BETWEEN @From AND @To AND IsDeleted = 0",
            new { From = from, To = to });

        var report = new RevenueReportDto(totalRevenue, fuelExpense, maintenanceCost, totalRevenue - fuelExpense - maintenanceCost);
        return ApiResponse<RevenueReportDto>.SuccessResponse(report);
    }
}

public record GetVehicleProfitQuery(DateTime? FromDate, DateTime? ToDate, int? VehicleId)
    : IRequest<ApiResponse<List<VehicleProfitDto>>>;

public class GetVehicleProfitQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetVehicleProfitQuery, ApiResponse<List<VehicleProfitDto>>>
{
    public async Task<ApiResponse<List<VehicleProfitDto>>> Handle(GetVehicleProfitQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = request.ToDate ?? DateTime.UtcNow;

        var results = await connection.QueryAsync<VehicleProfitDto>(
            @"SELECT v.Id AS VehicleId, v.Name AS VehicleName,
              ISNULL(SUM(p.Amount), 0) AS Revenue,
              ISNULL((SELECT SUM(fl.TotalCost) FROM FuelLogs fl WHERE fl.VehicleId = v.Id AND fl.FuelDate BETWEEN @From AND @To AND fl.IsDeleted = 0), 0) AS FuelCost,
              ISNULL((SELECT SUM(m.Cost) FROM Maintenance m WHERE m.VehicleId = v.Id AND m.MaintenanceDate BETWEEN @From AND @To AND m.IsDeleted = 0), 0) AS MaintenanceCost,
              ISNULL(SUM(p.Amount), 0)
                - ISNULL((SELECT SUM(fl.TotalCost) FROM FuelLogs fl WHERE fl.VehicleId = v.Id AND fl.FuelDate BETWEEN @From AND @To AND fl.IsDeleted = 0), 0)
                - ISNULL((SELECT SUM(m.Cost) FROM Maintenance m WHERE m.VehicleId = v.Id AND m.MaintenanceDate BETWEEN @From AND @To AND m.IsDeleted = 0), 0) AS Profit
              FROM Vehicles v
              LEFT JOIN Bookings b ON b.VehicleId = v.Id AND b.IsDeleted = 0 AND b.CreatedAt BETWEEN @From AND @To
              LEFT JOIN Payments p ON p.BookingId = b.Id AND p.Status = @Paid AND p.IsDeleted = 0
              WHERE v.IsDeleted = 0 AND (@VehicleId IS NULL OR v.Id = @VehicleId)
              GROUP BY v.Id, v.Name",
            new { From = from, To = to, Paid = (int)PaymentStatus.Paid, request.VehicleId });

        return ApiResponse<List<VehicleProfitDto>>.SuccessResponse(results.ToList());
    }
}

public record GetDriverPerformanceQuery(DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<DriverPerformanceDto>>>;

public class GetDriverPerformanceQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDriverPerformanceQuery, ApiResponse<List<DriverPerformanceDto>>>
{
    public async Task<ApiResponse<List<DriverPerformanceDto>>> Handle(GetDriverPerformanceQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = request.ToDate ?? DateTime.UtcNow;

        var results = await connection.QueryAsync<DriverPerformanceDto>(
            @"SELECT d.Id AS DriverId, d.FullName AS DriverName,
              COUNT(b.Id) AS TotalTrips,
              SUM(CASE WHEN b.Status = @Completed THEN 1 ELSE 0 END) AS CompletedTrips,
              ISNULL(SUM(CASE WHEN b.Status = @Completed THEN b.TotalAmount ELSE 0 END), 0) AS TotalRevenue
              FROM Drivers d
              LEFT JOIN Bookings b ON b.DriverId = d.Id AND b.IsDeleted = 0 AND b.CreatedAt BETWEEN @From AND @To
              WHERE d.IsDeleted = 0
              GROUP BY d.Id, d.FullName
              ORDER BY CompletedTrips DESC",
            new { Completed = (int)BookingStatus.Completed, From = from, To = to });

        return ApiResponse<List<DriverPerformanceDto>>.SuccessResponse(results.ToList());
    }
}
