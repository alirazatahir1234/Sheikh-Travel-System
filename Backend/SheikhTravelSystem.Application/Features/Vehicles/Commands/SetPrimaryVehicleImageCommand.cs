using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record SetPrimaryVehicleImageCommand(int VehicleId, int DocumentId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => VehicleId;
}

public class SetPrimaryVehicleImageCommandValidator : AbstractValidator<SetPrimaryVehicleImageCommand>
{
    public SetPrimaryVehicleImageCommandValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.DocumentId).GreaterThan(0);
    }
}

public class SetPrimaryVehicleImageCommandHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<SetPrimaryVehicleImageCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SetPrimaryVehicleImageCommand request, CancellationToken cancellationToken)
    {
        await vehicleRepository.SetPrimaryImageAsync(
            request.VehicleId, request.DocumentId, tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Display photo updated.");
    }
}
