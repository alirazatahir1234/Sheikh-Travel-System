using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record ListWorkshopsQuery : IRequest<ApiResponse<IReadOnlyList<WorkshopDto>>>;

public record CreateWorkshopCommand(CreateWorkshopDto Body) : IRequest<ApiResponse<int>>;

public class ListWorkshopsQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListWorkshopsQuery, ApiResponse<IReadOnlyList<WorkshopDto>>>
{
    public Task<ApiResponse<IReadOnlyList<WorkshopDto>>> Handle(ListWorkshopsQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListWorkshopsAsync(request, cancellationToken);
}

public class CreateWorkshopCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<CreateWorkshopCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateWorkshopCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.CreateWorkshopAsync(request, cancellationToken);
}

public record ListServiceTypesQuery : IRequest<ApiResponse<IReadOnlyList<ServiceTypeDto>>>;

public class ListServiceTypesQueryHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ListServiceTypesQuery, ApiResponse<IReadOnlyList<ServiceTypeDto>>>
{
    public Task<ApiResponse<IReadOnlyList<ServiceTypeDto>>> Handle(ListServiceTypesQuery request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ListServiceTypesAsync(request, cancellationToken);
}
