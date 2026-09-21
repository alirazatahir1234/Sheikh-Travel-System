using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record GetWorkshopByIdQuery(int Id) : IRequest<ApiResponse<WorkshopDto>>;

public record UpdateWorkshopCommand(int Id, UpdateWorkshopDto Body) : IRequest<ApiResponse<bool>>;

public record SetWorkshopActiveCommand(int Id, bool IsActive) : IRequest<ApiResponse<bool>>;

public class GetWorkshopByIdQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<GetWorkshopByIdQuery, ApiResponse<WorkshopDto>>
{
    public Task<ApiResponse<WorkshopDto>> Handle(GetWorkshopByIdQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.GetWorkshopByIdAsync(request, cancellationToken);
}

public class UpdateWorkshopCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<UpdateWorkshopCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateWorkshopCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.UpdateWorkshopAsync(request, cancellationToken);
}

public class SetWorkshopActiveCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<SetWorkshopActiveCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(SetWorkshopActiveCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.SetWorkshopActiveAsync(request, cancellationToken);
}
