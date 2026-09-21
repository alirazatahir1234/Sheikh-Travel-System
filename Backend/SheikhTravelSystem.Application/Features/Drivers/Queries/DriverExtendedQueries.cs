using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriverTimelineQuery(int DriverId) : IRequest<ApiResponse<IReadOnlyList<DriverTimelineEventDto>>>;

public class GetDriverTimelineQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverTimelineQuery, ApiResponse<IReadOnlyList<DriverTimelineEventDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<DriverTimelineEventDto>>> Handle(
        GetDriverTimelineQuery request, CancellationToken cancellationToken)
    {
        var events = await driverRepository.GetTimelineAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, cancellationToken);
        return ApiResponse<IReadOnlyList<DriverTimelineEventDto>>.SuccessResponse(events);
    }
}

public record GetDriverActiveDutyQuery(int DriverId) : IRequest<ApiResponse<DriverActiveDutyDto>>;

public record DriverActiveDutyDto(
    IReadOnlyList<DriverTripSummaryDto> RecentTrips,
    int FuelLogCount,
    bool HasGpsAssignment);

public record DriverTripSummaryDto(int Id, string Status, DateTime? TripDate, string? Route);

public class GetDriverActiveDutyQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverActiveDutyQuery, ApiResponse<DriverActiveDutyDto>>
{
    public async Task<ApiResponse<DriverActiveDutyDto>> Handle(
        GetDriverActiveDutyQuery request, CancellationToken cancellationToken)
    {
        var (trips, fuelCount, hasGps) = await driverRepository.GetActiveDutyAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, cancellationToken);
        var mapped = trips.Select(t => new DriverTripSummaryDto(t.Id, t.Status, t.TripDate, t.Route)).ToList();
        return ApiResponse<DriverActiveDutyDto>.SuccessResponse(
            new DriverActiveDutyDto(mapped, fuelCount, hasGps));
    }
}

public record GetDriverAssignmentsQuery(int DriverId, int Page = 1, int PageSize = 20)
    : IRequest<ApiResponse<PagedResult<DriverAssignmentDto>>>;

public class GetDriverAssignmentsQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverAssignmentsQuery, ApiResponse<PagedResult<DriverAssignmentDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverAssignmentDto>>> Handle(
        GetDriverAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var result = await driverRepository.GetAssignmentsAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PagedResult<DriverAssignmentDto>>.SuccessResponse(result);
    }
}

public record GetDriversAvailabilityQuery(int? BranchId = null)
    : IRequest<ApiResponse<DriversAvailabilitySummaryDto>>;

public class GetDriversAvailabilityQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriversAvailabilityQuery, ApiResponse<DriversAvailabilitySummaryDto>>
{
    public async Task<ApiResponse<DriversAvailabilitySummaryDto>> Handle(
        GetDriversAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var row = await driverRepository.GetAvailabilitySummaryAsync(
            tenantContext.GetRequiredTenantId(), request.BranchId, cancellationToken);
        return ApiResponse<DriversAvailabilitySummaryDto>.SuccessResponse(row);
    }
}

public record GetDriverAvailabilityDetailQuery(int DriverId)
    : IRequest<ApiResponse<DriverAvailabilityDetailDto>>;

public class GetDriverAvailabilityDetailQueryHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<GetDriverAvailabilityDetailQuery, ApiResponse<DriverAvailabilityDetailDto>>
{
    public async Task<ApiResponse<DriverAvailabilityDetailDto>> Handle(
        GetDriverAvailabilityDetailQuery request, CancellationToken cancellationToken)
    {
        var detail = await driverRepository.GetAvailabilityDetailAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, cancellationToken);
        if (detail is null)
            return ApiResponse<DriverAvailabilityDetailDto>.FailResponse("Driver not found.");
        return ApiResponse<DriverAvailabilityDetailDto>.SuccessResponse(detail);
    }
}
