using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Maintenance.Commands;

public record UpdateMaintenanceStatusCommand(int Id, MaintenanceStatus Status) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Maintenance";
    public int? AuditEntityId => Id;
}

public class UpdateMaintenanceStatusCommandValidator : AbstractValidator<UpdateMaintenanceStatusCommand>
{
    public UpdateMaintenanceStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateMaintenanceStatusCommandHandler(IMaintenanceRepository maintenanceRepository)
    : IRequestHandler<UpdateMaintenanceStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMaintenanceStatusCommand request, CancellationToken cancellationToken)
    {
        await maintenanceRepository.UpdateStatusAsync(request.Id, request.Status, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Maintenance status updated successfully.");
    }
}
