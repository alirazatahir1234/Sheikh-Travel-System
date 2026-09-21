using MediatR;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Queries;

public record GetDriverScoreRankingQuery(
    DateTime? FromDate,
    DateTime? ToDate,
    int? BranchId = null,
    int? DepartmentId = null)
    : IRequest<ApiResponse<List<DriverScoreDto>>>;

/// <summary>
/// Attributes GpsTrips to drivers via an AssignmentHistory time-window overlap join (GpsTrips has no
/// DriverId column). Overspeed comes directly from GpsAlertEvents.DriverId (already resolved at
/// write time by GpsAlertWriter — no join needed there). Idle/harsh-event factors reuse the same
/// uncapped per-device Traccar fetch pattern as the Day 3 Idle/Stop analytics, attributed to a
/// driver via the same vehicle+timestamp window lookup used for trips.
/// </summary>
public class GetDriverScoreRankingQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetDriverScoreRankingQuery, ApiResponse<List<DriverScoreDto>>>
{
    public Task<ApiResponse<List<DriverScoreDto>>> Handle(GetDriverScoreRankingQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetDriverScoreRankingAsync(request, cancellationToken);
}
