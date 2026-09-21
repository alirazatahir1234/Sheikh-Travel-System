using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverPerformanceSummaryQuery(int DriverId, DateTime? FromDate = null, DateTime? ToDate = null)
    : IRequest<ApiResponse<DriverPerformanceSummaryDto>>;

public class GetDriverPerformanceSummaryQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverPerformanceSummaryQuery, ApiResponse<DriverPerformanceSummaryDto>>
{
    public async Task<ApiResponse<DriverPerformanceSummaryDto>> Handle(
        GetDriverPerformanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var from = request.FromDate ?? DateTime.UtcNow.AddMonths(-3);
        var to = request.ToDate ?? DateTime.UtcNow;
        var summary = await driverRepository.GetPerformanceSummaryAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, from, to, cancellationToken);
        if (summary is null)
            return ApiResponse<DriverPerformanceSummaryDto>.FailResponse("Driver not found.");
        return ApiResponse<DriverPerformanceSummaryDto>.SuccessResponse(summary);
    }
}

public record GetDriverViolationsQuery(int DriverId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<DriverViolationDto>>>;

public class GetDriverViolationsQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverViolationsQuery, ApiResponse<PagedResult<DriverViolationDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverViolationDto>>> Handle(
        GetDriverViolationsQuery request, CancellationToken cancellationToken)
    {
        var result = await driverRepository.GetViolationsAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PagedResult<DriverViolationDto>>.SuccessResponse(result);
    }
}

public record GetDriverAttendanceQuery(int DriverId, DateTime? FromDate = null, DateTime? ToDate = null)
    : IRequest<ApiResponse<List<DriverAttendanceDto>>>;

public class GetDriverAttendanceQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverAttendanceQuery, ApiResponse<List<DriverAttendanceDto>>>
{
    public async Task<ApiResponse<List<DriverAttendanceDto>>> Handle(
        GetDriverAttendanceQuery request, CancellationToken cancellationToken)
    {
        var from = (request.FromDate ?? DateTime.UtcNow.AddDays(-30)).Date;
        var to = (request.ToDate ?? DateTime.UtcNow).Date;
        var items = await driverRepository.GetAttendanceAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, from, to, cancellationToken);
        return ApiResponse<List<DriverAttendanceDto>>.SuccessResponse(items.ToList());
    }
}

public record GetDriverLocationQuery(int DriverId) : IRequest<ApiResponse<DriverLocationDto>>;

public class GetDriverLocationQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverLocationQuery, ApiResponse<DriverLocationDto>>
{
    public async Task<ApiResponse<DriverLocationDto>> Handle(GetDriverLocationQuery request, CancellationToken cancellationToken)
    {
        var row = await driverRepository.GetLocationAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, cancellationToken);
        if (row is null)
            return ApiResponse<DriverLocationDto>.FailResponse("No active vehicle assignment for GPS tracking.");
        return ApiResponse<DriverLocationDto>.SuccessResponse(row);
    }
}

public record GetDriverLocationHistoryQuery(int DriverId, DateTime From, DateTime To)
    : IRequest<ApiResponse<List<DriverLocationPointDto>>>;

public class GetDriverLocationHistoryQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverLocationHistoryQuery, ApiResponse<List<DriverLocationPointDto>>>
{
    public async Task<ApiResponse<List<DriverLocationPointDto>>> Handle(
        GetDriverLocationHistoryQuery request, CancellationToken cancellationToken)
    {
        var points = await driverRepository.GetLocationHistoryAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.From, request.To, cancellationToken);
        return ApiResponse<List<DriverLocationPointDto>>.SuccessResponse(points.ToList());
    }
}
