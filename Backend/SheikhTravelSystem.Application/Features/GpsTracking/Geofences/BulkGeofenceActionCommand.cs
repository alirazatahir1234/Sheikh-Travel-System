using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Assign vehicles, move to a group, enable, disable or delete several geofences at once.</summary>
public record BulkGeofenceActionCommand(BulkGeofenceActionDto Body) : IRequest<ApiResponse<BulkGeofenceResultDto>>, IAuditableCommand
{
    public string AuditAction => $"Bulk {Body.Action}";
    public string AuditEntityName => "Geofence";
    public int? AuditEntityId => null;
}

public class BulkGeofenceActionCommandValidator : AbstractValidator<BulkGeofenceActionCommand>
{
    public BulkGeofenceActionCommandValidator()
    {
        RuleFor(x => x.Body.Ids).NotEmpty().Must(ids => ids.Length <= 500)
            .WithMessage("Pick between 1 and 500 geofences.");
        RuleFor(x => x.Body.Action).Must(a => GeofenceBulkAction.Values.Contains(a))
            .WithMessage("Action must be assign, group, enable, disable or delete.");
        RuleFor(x => x.Body.VehicleIds).NotEmpty().When(x => x.Body.Action == GeofenceBulkAction.Assign)
            .WithMessage("Pick the vehicles to assign.");
    }
}

public class BulkGeofenceActionCommandHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<BulkGeofenceActionCommand, ApiResponse<BulkGeofenceResultDto>>
{
    public Task<ApiResponse<BulkGeofenceResultDto>> Handle(BulkGeofenceActionCommand request, CancellationToken cancellationToken)
        => repository.BulkAsync(request.Body, cancellationToken);
}
