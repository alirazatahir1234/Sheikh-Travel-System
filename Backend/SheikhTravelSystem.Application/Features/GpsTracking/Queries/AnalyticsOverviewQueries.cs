using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetAnalyticsOverviewQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null,
    int? DriverId = null)
    : IRequest<ApiResponse<AnalyticsOverviewDto>>;

/// <summary>
/// Composition over existing queries — no new aggregation logic beyond the overspeed-today count
/// and the live utilization estimate. Fans out via Task.WhenAll the same way
/// GetFleetTripSummaryQuery/GetTripAnalyticsQuery already do for their own sub-fetches.
/// </summary>
public class GetAnalyticsOverviewQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetAnalyticsOverviewQuery, ApiResponse<AnalyticsOverviewDto>>
{
    public Task<ApiResponse<AnalyticsOverviewDto>> Handle(GetAnalyticsOverviewQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetAnalyticsOverviewAsync(request, cancellationToken);
}
