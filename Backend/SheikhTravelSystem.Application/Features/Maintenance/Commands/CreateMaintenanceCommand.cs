using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;

namespace SheikhTravelSystem.Application.Features.Maintenance.Commands;

public record CreateMaintenanceCommand(CreateMaintenanceDto Maintenance) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Maintenance";
    public int? AuditEntityId => null;
}

public class CreateMaintenanceCommandValidator : AbstractValidator<CreateMaintenanceCommand>
{
    public CreateMaintenanceCommandValidator()
    {
        RuleFor(x => x.Maintenance.VehicleId).GreaterThan(0);
        RuleFor(x => x.Maintenance.Description).NotEmpty();
        RuleFor(x => x.Maintenance.Cost).GreaterThanOrEqualTo(0);
    }
}

public class CreateMaintenanceCommandHandler(IMaintenanceRepository maintenanceRepository)
    : IRequestHandler<CreateMaintenanceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateMaintenanceCommand request, CancellationToken cancellationToken)
    {
        var id = await maintenanceRepository.CreateAsync(request.Maintenance, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Maintenance record created successfully.");
    }
}
