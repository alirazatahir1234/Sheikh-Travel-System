using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>
/// Visits report rows, server-paged. From/To are local days (inclusive); without both the result is empty, never
/// unfiltered. VehicleIds is a comma list.
/// </summary>
public record GetGeofenceVisitsQuery(
    int? GeofenceId,
    string? VehicleIds,
    int? DriverId,
    DateOnly? From,
    DateOnly? To,
    string? Status,
    int Page = 1,
    int PageSize = 25) : IRequest<ApiResponse<GeofenceVisitsPageDto>>;

public class GetGeofenceVisitsQueryValidator : AbstractValidator<GetGeofenceVisitsQuery>
{
    public GetGeofenceVisitsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).Must(s => s is null || s == "all" || GeofenceVisitStatus.Values.Contains(s))
            .WithMessage("Unknown status.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To)
            .WithMessage("From must be on or before To.");
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.To.Value.DayNumber - x.From.Value.DayNumber <= 366)
            .WithMessage("Pick at most one year.");
    }
}

public class GetGeofenceVisitsQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<GetGeofenceVisitsQuery, ApiResponse<GeofenceVisitsPageDto>>
{
    public async Task<ApiResponse<GeofenceVisitsPageDto>> Handle(GetGeofenceVisitsQuery request, CancellationToken cancellationToken)
    {
        var filter = new GeofenceVisitFilter(
            request.GeofenceId, GeofenceIdList.Parse(request.VehicleIds), request.DriverId, request.From, request.To,
            request.Status is null or "all" ? null : request.Status);
        return ApiResponse<GeofenceVisitsPageDto>.SuccessResponse(
            await repository.GetVisitsAsync(filter, request.Page, request.PageSize, cancellationToken));
    }
}

/// <summary>Parses "1,2,3" query lists; blanks and non-numbers are dropped.</summary>
public static class GeofenceIdList
{
    public static IReadOnlyList<int>? Parse(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return null;

        var ids = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        return ids.Count == 0 ? null : ids;
    }
}
