using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Routes.Commands;

public record DeleteRouteCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Route";
    public int? AuditEntityId => Id;
}

public class DeleteRouteCommandHandler(IRouteRepository routeRepository)
    : IRequestHandler<DeleteRouteCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        await routeRepository.DeleteAsync(request.Id, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Route deleted successfully.");
    }
}
