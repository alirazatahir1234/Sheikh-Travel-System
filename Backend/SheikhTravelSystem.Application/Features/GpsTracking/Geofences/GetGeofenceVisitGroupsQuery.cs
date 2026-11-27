using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>
/// The visits report grouped by vehicle, driver, geofence or local day, for the same filters as the table. Without
/// both From and To the result is empty, never unfiltered.
/// </summary>
public record GetGeofenceVisitGroupsQuery(
    string GroupBy,
    int? GeofenceId,
    string? VehicleIds,
    int? DriverId,
    DateOnly? From,
    DateOnly? To,
    string? Status) : IRequest<ApiResponse<GeofenceVisitGroupsDto>>;

public class GetGeofenceVisitGroupsQueryValidator : AbstractValidator<GetGeofenceVisitGroupsQuery>
{
    public GetGeofenceVisitGroupsQueryValidator()
    {
        RuleFor(x => x.GroupBy).Must(g => GeofenceVisitGroupBy.Values.Contains(g))
            .WithMessage("Group by must be vehicle, driver, geofence or day.");
        RuleFor(x => x.Status).Must(s => s is null || s == "all" || GeofenceVisitStatus.Values.Contains(s))
            .WithMessage("Unknown status.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("From must be on or before To.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber <= 366)
            .WithMessage("Pick at most one year.");
    }
}

public class GetGeofenceVisitGroupsQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceVisitGroupsQuery, ApiResponse<GeofenceVisitGroupsDto>>
{
    public async Task<ApiResponse<GeofenceVisitGroupsDto>> Handle(GetGeofenceVisitGroupsQuery request, CancellationToken cancellationToken)
    {
        var filter = new GeofenceVisitFilter(
            request.GeofenceId, GeofenceIdList.Parse(request.VehicleIds), request.DriverId, request.From, request.To,
            request.Status is null or "all" ? null : request.Status);
        return ApiResponse<GeofenceVisitGroupsDto>.SuccessResponse(
            await repository.GetVisitGroupsAsync(filter, request.GroupBy, cancellationToken));
    }
}
