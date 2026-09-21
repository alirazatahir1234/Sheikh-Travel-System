using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetFuelAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<FuelAnalyticsDto>>;

/// <summary>
/// Fuel *cost* data already exists (FuelLogs, manual entries). Fuel *consumption efficiency* is
/// computed fresh here by cross-referencing FuelLogs.Liters with GpsTrips.DistanceKm over the same
/// range/filters — the first place in this codebase these two previously-unlinked data sources are
/// joined. GPS FuelLevel telemetry (Traccar) is a separate, still-unlinked signal — not used here.
/// </summary>
public class GetFuelAnalyticsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetFuelAnalyticsQuery, ApiResponse<FuelAnalyticsDto>>
{
    public Task<ApiResponse<FuelAnalyticsDto>> Handle(GetFuelAnalyticsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetFuelAnalyticsAsync(request, cancellationToken);
}

public record GetCostAnalyticsQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null)
    : IRequest<ApiResponse<CostAnalyticsDto>>;

/// <summary>Cost/KM = (Fuel + Maintenance) / Distance only — confirmed scope decision, not a placeholder pending more data sources. CostBasisNote is rendered verbatim in the UI so this limitation is never silently hidden.</summary>
public class GetCostAnalyticsQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetCostAnalyticsQuery, ApiResponse<CostAnalyticsDto>>
{
    public Task<ApiResponse<CostAnalyticsDto>> Handle(GetCostAnalyticsQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetCostAnalyticsAsync(request, cancellationToken);
}
