using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.Maintenance.Commands;

public record DeleteMaintenanceCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Maintenance";
    public int? AuditEntityId => Id;
}

public class DeleteMaintenanceCommandValidator : AbstractValidator<DeleteMaintenanceCommand>
{
    public DeleteMaintenanceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class DeleteMaintenanceCommandHandler(IMaintenanceRepository maintenanceRepository)
    : IRequestHandler<DeleteMaintenanceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteMaintenanceCommand request, CancellationToken cancellationToken)
    {
        await maintenanceRepository.DeleteAsync(request.Id, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Maintenance record deleted successfully.");
    }
}
