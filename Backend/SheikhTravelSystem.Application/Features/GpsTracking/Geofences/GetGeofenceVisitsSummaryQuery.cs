using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Visits report KPIs and today's occupancy for the same filters as the table (status excluded).</summary>
public record GetGeofenceVisitsSummaryQuery(
    int? GeofenceId,
    string? VehicleIds,
    int? DriverId,
    DateOnly? From,
    DateOnly? To) : IRequest<ApiResponse<GeofenceVisitsSummaryDto>>;

public class GetGeofenceVisitsSummaryQueryValidator : AbstractValidator<GetGeofenceVisitsSummaryQuery>
{
    public GetGeofenceVisitsSummaryQueryValidator()
    {
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("From must be on or before To.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber <= 366)
            .WithMessage("Pick at most one year.");
    }
}

public class GetGeofenceVisitsSummaryQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceVisitsSummaryQuery, ApiResponse<GeofenceVisitsSummaryDto>>
{
    public async Task<ApiResponse<GeofenceVisitsSummaryDto>> Handle(GetGeofenceVisitsSummaryQuery request, CancellationToken cancellationToken)
    {
        var filter = new GeofenceVisitFilter(
            request.GeofenceId, GeofenceIdList.Parse(request.VehicleIds), request.DriverId, request.From, request.To, null);
        return ApiResponse<GeofenceVisitsSummaryDto>.SuccessResponse(
            await repository.GetVisitsSummaryAsync(filter, cancellationToken));
    }
}
