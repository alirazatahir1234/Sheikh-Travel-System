using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>A geofence's entries, exits and rule alerts on one local day (today when no day is given), newest first.</summary>
public record GetGeofenceActivityQuery(int Id, DateOnly? Day) : IRequest<ApiResponse<IReadOnlyList<GeofenceActivityItemDto>>>;

public class GetGeofenceActivityQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceActivityQuery, ApiResponse<IReadOnlyList<GeofenceActivityItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<GeofenceActivityItemDto>>> Handle(GetGeofenceActivityQuery request, CancellationToken cancellationToken)
    {
        var activity = await repository.GetActivityAsync(request.Id, request.Day, cancellationToken);
        return activity is null
            ? ApiResponse<IReadOnlyList<GeofenceActivityItemDto>>.FailResponse("Geofence not found.")
            : ApiResponse<IReadOnlyList<GeofenceActivityItemDto>>.SuccessResponse(activity);
    }
}
