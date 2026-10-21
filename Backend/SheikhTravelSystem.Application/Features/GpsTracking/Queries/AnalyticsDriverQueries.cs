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
/// DriverId column). Overspeed and harsh brake/accel come from GpsAlertEvents.DriverId (harsh
/// persisted by Traccar event sync). Idle still uses live Traccar stops; live harsh is only a
/// fallback when no local harsh rows exist yet for the range.
/// </summary>
public class GetDriverScoreRankingQueryHandler(IGpsTrackingRepository gpsTrackingRepository)
    : IRequestHandler<GetDriverScoreRankingQuery, ApiResponse<List<DriverScoreDto>>>
{
    public Task<ApiResponse<List<DriverScoreDto>>> Handle(GetDriverScoreRankingQuery request, CancellationToken cancellationToken)
        => gpsTrackingRepository.GetDriverScoreRankingAsync(request, cancellationToken);
}
