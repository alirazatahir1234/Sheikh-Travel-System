using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public record CreateMaintenanceRequestCommand(CreateMaintenanceRequestDto Body)
    : IRequest<ApiResponse<int>>;

public record UpdateMaintenanceRequestCommand(int Id, UpdateMaintenanceRequestDto Body)
    : IRequest<ApiResponse<bool>>;

public record ConvertMaintenanceRequestCommand(int Id, ConvertRequestToWorkOrderDto Body)
    : IRequest<ApiResponse<int>>;

public class CreateMaintenanceRequestCommandValidator : AbstractValidator<CreateMaintenanceRequestCommand>
{
    public CreateMaintenanceRequestCommandValidator()
    {
        RuleFor(x => x.Body.VehicleId).GreaterThan(0).WithMessage("Vehicle is required.");

        RuleFor(x => x.Body.Description)
            .Must(d => !string.IsNullOrWhiteSpace(d))
            .WithMessage("Description is required.")
            .Must(d => d!.Trim().Length >= MaintenanceRequestValidation.DescriptionMinLength)
            .WithMessage($"Description must be at least {MaintenanceRequestValidation.DescriptionMinLength} characters.")
            .Must(d => d!.Trim().Length <= MaintenanceRequestValidation.DescriptionMaxLength)
            .WithMessage($"Description cannot exceed {MaintenanceRequestValidation.DescriptionMaxLength} characters.");

        RuleFor(x => x.Body.Priority)
            .NotEmpty().WithMessage("Priority is required.")
            .Must(MaintenanceRequestValidation.IsValidPriority)
            .WithMessage("Priority is invalid.");

        RuleFor(x => x.Body.RequestType)
            .NotEmpty().WithMessage("Type is required.")
            .Must(MaintenanceRequestValidation.IsValidRequestType)
            .WithMessage("Request type is invalid.");

        RuleFor(x => x.Body.IssueCategory)
            .NotEmpty().WithMessage("Category is required.")
            .Must(MaintenanceRequestValidation.IsValidIssueCategory)
            .WithMessage("Issue category is invalid.");
    }
}

public class CreateMaintenanceRequestCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<CreateMaintenanceRequestCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.CreateMaintenanceRequestAsync(request, cancellationToken);
}

public class UpdateMaintenanceRequestCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<UpdateMaintenanceRequestCommand, ApiResponse<bool>>
{
    public Task<ApiResponse<bool>> Handle(UpdateMaintenanceRequestCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.UpdateMaintenanceRequestAsync(request, cancellationToken);
}

public class ConvertMaintenanceRequestCommandHandler(IMaintenanceModuleRepository maintenanceModuleRepository)
    : IRequestHandler<ConvertMaintenanceRequestCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(ConvertMaintenanceRequestCommand request, CancellationToken cancellationToken)
        => maintenanceModuleRepository.ConvertMaintenanceRequestAsync(request, cancellationToken);
}
