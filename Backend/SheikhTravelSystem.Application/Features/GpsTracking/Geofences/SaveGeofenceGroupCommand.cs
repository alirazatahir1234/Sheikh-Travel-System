using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Creates (Id null) or renames a geofence group.</summary>
public record SaveGeofenceGroupCommand(int? Id, SaveGeofenceGroupDto Group) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id is null ? "Create" : "Update";
    public string AuditEntityName => "GeofenceGroup";
    public int? AuditEntityId => Id;
}

public class SaveGeofenceGroupCommandValidator : AbstractValidator<SaveGeofenceGroupCommand>
{
    public SaveGeofenceGroupCommandValidator()
    {
        RuleFor(x => x.Group.Name).NotEmpty().MaximumLength(100).WithMessage("Group name is required (max 100 characters).");
        RuleFor(x => x.Group.SortOrder).InclusiveBetween(0, 10_000);
    }
}

public class SaveGeofenceGroupCommandHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<SaveGeofenceGroupCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(SaveGeofenceGroupCommand request, CancellationToken cancellationToken)
        => repository.SaveGroupAsync(request.Id, request.Group, cancellationToken);
}
