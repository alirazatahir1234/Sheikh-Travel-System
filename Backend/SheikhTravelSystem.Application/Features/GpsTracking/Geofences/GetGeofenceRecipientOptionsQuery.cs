using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Roles and active users of the company for the Notify step (names only, no contact details).</summary>
public record GetGeofenceRecipientOptionsQuery : IRequest<ApiResponse<GeofenceRecipientOptionsDto>>;

public class GetGeofenceRecipientOptionsQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceRecipientOptionsQuery, ApiResponse<GeofenceRecipientOptionsDto>>
{
    public async Task<ApiResponse<GeofenceRecipientOptionsDto>> Handle(GetGeofenceRecipientOptionsQuery request, CancellationToken cancellationToken)
        => ApiResponse<GeofenceRecipientOptionsDto>.SuccessResponse(await repository.GetRecipientOptionsAsync(cancellationToken));
}
