using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Geofences;

/// <summary>Deletes a group; its geofences become ungrouped (they are not deleted).</summary>
public record DeleteGeofenceGroupCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "GeofenceGroup";
    public int? AuditEntityId => Id;
}

public class DeleteGeofenceGroupCommandHandler(IGeofenceWorkspaceRepository repository)
    : IRequestHandler<DeleteGeofenceGroupCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(DeleteGeofenceGroupCommand request, CancellationToken cancellationToken)
        => repository.DeleteGroupAsync(request.Id, cancellationToken);
}
