using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>The six KPI cards on the geofences screen, from one call (ERP rule: no KPIs added up in the browser).</summary>
public record GetGeofenceSummaryQuery : IRequest<ApiResponse<GeofenceSummaryDto>>;

public class GetGeofenceSummaryQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceSummaryQuery, ApiResponse<GeofenceSummaryDto>>
{
    public async Task<ApiResponse<GeofenceSummaryDto>> Handle(GetGeofenceSummaryQuery request, CancellationToken cancellationToken)
        => ApiResponse<GeofenceSummaryDto>.SuccessResponse(await repository.GetSummaryAsync(cancellationToken));
}
