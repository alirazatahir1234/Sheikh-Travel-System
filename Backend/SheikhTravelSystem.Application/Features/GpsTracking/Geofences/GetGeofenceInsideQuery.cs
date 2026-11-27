using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Vehicles inside a geofence now, with how long they have been there.</summary>
public record GetGeofenceInsideQuery(int Id) : IRequest<ApiResponse<IReadOnlyList<GeofenceInsideVehicleDto>>>;

public class GetGeofenceInsideQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceInsideQuery, ApiResponse<IReadOnlyList<GeofenceInsideVehicleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GeofenceInsideVehicleDto>>> Handle(GetGeofenceInsideQuery request, CancellationToken cancellationToken)
    {
        var inside = await repository.GetInsideAsync(request.Id, cancellationToken);
        return inside is null
            ? ApiResponse<IReadOnlyList<GeofenceInsideVehicleDto>>.FailResponse("Geofence not found.")
            : ApiResponse<IReadOnlyList<GeofenceInsideVehicleDto>>.SuccessResponse(inside);
    }
}
