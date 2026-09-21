using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;

namespace SheikhTravelSystem.Application.Features.Maintenance.Commands;

public record UpdateMaintenanceCommand(int Id, CreateMaintenanceDto Maintenance) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Maintenance";
    public int? AuditEntityId => Id;
}

public class UpdateMaintenanceCommandValidator : AbstractValidator<UpdateMaintenanceCommand>
{
    public UpdateMaintenanceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Maintenance.VehicleId).GreaterThan(0);
        RuleFor(x => x.Maintenance.Description).NotEmpty();
        RuleFor(x => x.Maintenance.Cost).GreaterThanOrEqualTo(0);
    }
}

public class UpdateMaintenanceCommandHandler(IMaintenanceRepository maintenanceRepository)
    : IRequestHandler<UpdateMaintenanceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMaintenanceCommand request, CancellationToken cancellationToken)
    {
        await maintenanceRepository.UpdateAsync(request.Id, request.Maintenance, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Maintenance record updated successfully.");
    }
}
