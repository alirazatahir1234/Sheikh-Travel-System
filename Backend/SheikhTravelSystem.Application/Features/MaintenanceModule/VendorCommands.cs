using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record CreateVendorCommand(CreateVendorDto Body) : IRequest<ApiResponse<int>>;

public record UpdateVendorCommand(int Id, UpdateVendorDto Body) : IRequest<ApiResponse<bool>>;

public record SetVendorActiveCommand(int Id, bool IsActive) : IRequest<ApiResponse<bool>>;

public class CreateVendorCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<CreateVendorCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateVendorCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.CreateVendorAsync(request, cancellationToken);
}

public class UpdateVendorCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<UpdateVendorCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateVendorCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.UpdateVendorAsync(request, cancellationToken);
}

public class SetVendorActiveCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<SetVendorActiveCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(SetVendorActiveCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.SetVendorActiveAsync(request, cancellationToken);
}
