using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

public record GetGeofenceGroupsQuery : IRequest<ApiResponse<IReadOnlyList<GeofenceGroupDto>>>;

public class GetGeofenceGroupsQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceGroupsQuery, ApiResponse<IReadOnlyList<GeofenceGroupDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GeofenceGroupDto>>> Handle(GetGeofenceGroupsQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GeofenceGroupDto>>.SuccessResponse(await repository.GetGroupsAsync(cancellationToken));
}
