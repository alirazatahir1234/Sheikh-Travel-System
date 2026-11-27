using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

public record GetGeofenceDetailQuery(int Id) : IRequest<ApiResponse<GeofenceDetailDto>>;

public class GetGeofenceDetailQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceDetailQuery, ApiResponse<GeofenceDetailDto>>
{
    public async Task<ApiResponse<GeofenceDetailDto>> Handle(GetGeofenceDetailQuery request, CancellationToken cancellationToken)
    {
        var detail = await repository.GetDetailAsync(request.Id, cancellationToken);
        return detail is null
            ? ApiResponse<GeofenceDetailDto>.FailResponse("Geofence not found.")
            : ApiResponse<GeofenceDetailDto>.SuccessResponse(detail);
    }
}
