using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record CreateWorkOrderCommand(CreateWorkOrderDto Body) : IRequest<ApiResponse<int>>;

public record UpdateWorkOrderStatusCommand(int Id, UpdateWorkOrderStatusDto Body) : IRequest<ApiResponse<bool>>;

public record UpdateWorkOrderCommand(int Id, UpdateWorkOrderDto Body) : IRequest<ApiResponse<bool>>;

public class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator()
    {
        RuleFor(x => x.Body.VehicleId)
            .GreaterThan(0)
            .WithMessage("Please select a vehicle.");

        RuleFor(x => x.Body.ServiceTypeName)
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage("Select at least one service item.");

        RuleFor(x => x.Body.Priority)
            .Must(WorkOrderValidation.IsValidPriority)
            .When(x => !string.IsNullOrWhiteSpace(x.Body.Priority))
            .WithMessage("Priority is invalid.");

        RuleFor(x => x.Body.MaintenanceType)
            .Must(WorkOrderValidation.IsValidMaintenanceType)
            .When(x => !string.IsNullOrWhiteSpace(x.Body.MaintenanceType))
            .WithMessage("Maintenance type is invalid.");

        RuleFor(x => x.Body.LaborCost)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Labor cost cannot be negative.")
            .LessThanOrEqualTo(WorkOrderValidation.MaxCost)
            .WithMessage($"Labor cost cannot exceed {WorkOrderValidation.MaxCost:N2}.");

        RuleFor(x => x.Body.PartsCost)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Parts cost cannot be negative.")
            .LessThanOrEqualTo(WorkOrderValidation.MaxCost)
            .WithMessage($"Parts cost cannot exceed {WorkOrderValidation.MaxCost:N2}.");

        RuleFor(x => x.Body.Notes)
            .MaximumLength(WorkOrderValidation.NotesMaxLength)
            .WithMessage($"Notes cannot exceed {WorkOrderValidation.NotesMaxLength} characters.");

        RuleFor(x => x.Body)
            .Must(b => WorkOrderValidation.IsValidDateRange(b.StartDate, b.EstimatedCompletionDate))
            .WithMessage("Estimated completion must be on or after the start date.");

        RuleFor(x => x.Body)
            .Must(b => !string.Equals(b.MaintenanceType, "Emergency", StringComparison.OrdinalIgnoreCase) || b.StartDate.HasValue)
            .WithMessage("Start date is required for emergency work orders.");
    }
}

public class CreateWorkOrderCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<CreateWorkOrderCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.CreateWorkOrderAsync(request, cancellationToken);
}

public class UpdateWorkOrderStatusCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<UpdateWorkOrderStatusCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateWorkOrderStatusCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.UpdateWorkOrderStatusAsync(request, cancellationToken);
}

public class UpdateWorkOrderCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<UpdateWorkOrderCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateWorkOrderCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.UpdateWorkOrderAsync(request, cancellationToken);
}
