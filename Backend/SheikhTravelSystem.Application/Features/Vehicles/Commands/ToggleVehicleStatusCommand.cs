using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record ToggleVehicleStatusCommand(int Id) : IRequest<ApiResponse<VehicleStatus>>, IAuditableCommand
{
    public string AuditAction => "ToggleStatus";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class ToggleVehicleStatusCommandValidator : AbstractValidator<ToggleVehicleStatusCommand>
{
    public ToggleVehicleStatusCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class ToggleVehicleStatusCommandHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<ToggleVehicleStatusCommand, ApiResponse<VehicleStatus>>
{
    public async Task<ApiResponse<VehicleStatus>> Handle(ToggleVehicleStatusCommand request, CancellationToken cancellationToken)
    {
        var newStatus = await vehicleRepository.ToggleStatusAsync(
            request.Id, tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<VehicleStatus>.SuccessResponse(newStatus, $"Vehicle status changed to {newStatus}.");
    }
}
