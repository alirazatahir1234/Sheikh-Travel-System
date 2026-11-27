using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>"Test with history": how many entries a draft shape would have seen in the last 7 days.</summary>
public record TestGeofenceShapeQuery(TestGeofenceShapeDto Shape) : IRequest<ApiResponse<TestGeofenceShapeResultDto>>;

public class TestGeofenceShapeQueryValidator : AbstractValidator<TestGeofenceShapeQuery>
{
    public TestGeofenceShapeQueryValidator()
    {
        RuleFor(x => x.Shape).Custom((shape, ctx) =>
        {
            if (!GpsGeoHelper.TryValidateGeofenceGeometry(
                    shape.AreaType, shape.RadiusMeters, shape.GeoJson, shape.CorridorWidthMeters, out var error) && error is not null)
                ctx.AddFailure(error);
        });
        RuleFor(x => x.Shape.BufferMeters).InclusiveBetween(0, 500);
        RuleFor(x => x.Shape.VehicleIds).Must(ids => ids is null || ids.Length <= 2_000);
    }
}

public class TestGeofenceShapeQueryHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<TestGeofenceShapeQuery, ApiResponse<TestGeofenceShapeResultDto>>
{
    public async Task<ApiResponse<TestGeofenceShapeResultDto>> Handle(TestGeofenceShapeQuery request, CancellationToken cancellationToken)
        => ApiResponse<TestGeofenceShapeResultDto>.SuccessResponse(await repository.TestShapeAsync(request.Shape, cancellationToken));
}
