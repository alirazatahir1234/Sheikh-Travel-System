using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Built-in types followed by the company's custom types.</summary>
public record GetGeofenceTypesQuery : IRequest<ApiResponse<IReadOnlyList<GeofenceTypeDto>>>;

public class GetGeofenceTypesQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceTypesQuery, ApiResponse<IReadOnlyList<GeofenceTypeDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GeofenceTypeDto>>> Handle(GetGeofenceTypesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<GeofenceTypeDto>>.SuccessResponse(await repository.GetTypesAsync(cancellationToken));
}
