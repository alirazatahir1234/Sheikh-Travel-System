using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Creates (Id null) or updates a geofence, its assignments, recipients and rules in one step.</summary>
public record SaveGeofenceCommand(int? Id, SaveGeofenceDto Geofence) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id is null ? "Create" : "Update";
    public string AuditEntityName => "Geofence";
    public int? AuditEntityId => Id;
}

public class SaveGeofenceCommandValidator : AbstractValidator<SaveGeofenceCommand>
{
    public SaveGeofenceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.Geofence).NotNull().SetValidator(new SaveGeofenceDtoValidator());
    }
}

public class SaveGeofenceCommandHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<SaveGeofenceCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(SaveGeofenceCommand request, CancellationToken cancellationToken)
        => repository.SaveAsync(request.Id, request.Geofence, cancellationToken);
}
