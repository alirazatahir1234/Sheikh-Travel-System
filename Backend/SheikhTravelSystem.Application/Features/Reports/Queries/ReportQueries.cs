using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Reports.DTOs;

namespace SheikhTravelSystem.Application.Features.Reports.Queries;

/// <summary>
/// Requests booking KPI report for a date range.
/// </summary>
public record GetBookingReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<BookingReportDto>>;

/// <summary>
/// Builds booking KPI aggregates.
/// </summary>
public class GetBookingReportQueryHandler(IReportRepository reportRepository)
    : IRequestHandler<GetBookingReportQuery, ApiResponse<BookingReportDto>>
{
    public async Task<ApiResponse<BookingReportDto>> Handle(GetBookingReportQuery request, CancellationToken cancellationToken)
    {
        // Default to recent 1-month window when filters are not provided.
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = request.ToDate ?? DateTime.UtcNow;

        var report = await reportRepository.GetBookingReportAsync(from, to, cancellationToken);
        return ApiResponse<BookingReportDto>.SuccessResponse(report);
    }
}

/// <summary>
/// Requests revenue/cost summary report for a date range.
/// </summary>
public record GetRevenueReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<RevenueReportDto>>;

/// <summary>
/// Builds revenue and expense aggregates.
/// </summary>
public class GetRevenueReportQueryHandler(IReportRepository reportRepository)
    : IRequestHandler<GetRevenueReportQuery, ApiResponse<RevenueReportDto>>
{
    public async Task<ApiResponse<RevenueReportDto>> Handle(GetRevenueReportQuery request, CancellationToken cancellationToken)
    {
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = request.ToDate ?? DateTime.UtcNow;

        var report = await reportRepository.GetRevenueReportAsync(from, to, cancellationToken);
        return ApiResponse<RevenueReportDto>.SuccessResponse(report);
    }
}

/// <summary>
/// Requests vehicle-level profitability report for a date range.
/// </summary>
public record GetVehicleProfitQuery(DateTime? FromDate, DateTime? ToDate, int? VehicleId)
    : IRequest<ApiResponse<List<VehicleProfitDto>>>;

/// <summary>
/// Computes profitability grouped by vehicle.
/// </summary>
public class GetVehicleProfitQueryHandler(IReportRepository reportRepository)
    : IRequestHandler<GetVehicleProfitQuery, ApiResponse<List<VehicleProfitDto>>>
{
    public async Task<ApiResponse<List<VehicleProfitDto>>> Handle(GetVehicleProfitQuery request, CancellationToken cancellationToken)
    {
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = request.ToDate ?? DateTime.UtcNow;

        var results = await reportRepository.GetVehicleProfitAsync(from, to, request.VehicleId, cancellationToken);
        return ApiResponse<List<VehicleProfitDto>>.SuccessResponse(results.ToList());
    }
}

/// <summary>
/// Requests driver performance report for a date range.
/// </summary>
public record GetDriverPerformanceQuery(DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<DriverPerformanceDto>>>;

/// <summary>
/// Computes trip completion and revenue metrics per driver.
/// </summary>
public class GetDriverPerformanceQueryHandler(IReportRepository reportRepository)
    : IRequestHandler<GetDriverPerformanceQuery, ApiResponse<List<DriverPerformanceDto>>>
{
    public async Task<ApiResponse<List<DriverPerformanceDto>>> Handle(GetDriverPerformanceQuery request, CancellationToken cancellationToken)
    {
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var to = request.ToDate ?? DateTime.UtcNow;

        var results = await reportRepository.GetDriverPerformanceAsync(from, to, cancellationToken);
        return ApiResponse<List<DriverPerformanceDto>>.SuccessResponse(results.ToList());
    }
}
