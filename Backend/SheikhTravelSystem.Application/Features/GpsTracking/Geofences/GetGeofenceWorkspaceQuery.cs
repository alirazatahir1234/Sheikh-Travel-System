using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Every geofence of the company with its group, type, coverage, inside-now and today's counts.</summary>
public record GetGeofenceWorkspaceQuery : IRequest<ApiResponse<IReadOnlyList<GeofenceListItemDto>>>;

public class GetGeofenceWorkspaceQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceWorkspaceQuery, ApiResponse<IReadOnlyList<GeofenceListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GeofenceListItemDto>>> Handle(GetGeofenceWorkspaceQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GeofenceListItemDto>>.SuccessResponse(await repository.GetListAsync(cancellationToken));
}
