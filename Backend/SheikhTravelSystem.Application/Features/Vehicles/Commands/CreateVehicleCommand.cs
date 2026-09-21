using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using static SheikhTravelSystem.Application.Features.Vehicles.VehicleDescriptiveTextRules;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record CreateVehicleCommand(CreateVehicleDto Vehicle, bool SaveAsDraft = false) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => null;
}

public class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        When(x => !x.SaveAsDraft, () =>
        {
            RuleFor(x => x.Vehicle.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Vehicle.RegistrationNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Vehicle.FuelAverage).GreaterThan(0);
            RuleFor(x => x.Vehicle.SeatingCapacity).GreaterThan(0);
        });
        RuleFor(x => x.Vehicle.VehicleCode).MaximumLength(40).When(x => x.Vehicle.VehicleCode != null);
        RuleFor(x => x.Vehicle.VIN).MaximumLength(64).When(x => x.Vehicle.VIN != null);

        RuleFor(x => x.Vehicle.Name)
            .Must(IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Vehicle.Name))
            .WithMessage("Vehicle name must be descriptive text, not numbers only.");
        RuleFor(x => x.Vehicle.Make)
            .Must(IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Vehicle.Make))
            .WithMessage("Make must be a valid manufacturer name.");
        RuleFor(x => x.Vehicle.Model)
            .Must(IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Vehicle.Model))
            .WithMessage("Model must be a valid model name.");
        RuleFor(x => x.Vehicle.Color)
            .Must(IsValid)
            .When(x => !string.IsNullOrWhiteSpace(x.Vehicle.Color))
            .WithMessage("Color must be a valid color name.");
    }
}

public class CreateVehicleCommandHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<CreateVehicleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        var id = await vehicleRepository.CreateAsync(
            tenantContext.GetRequiredTenantId(), request.Vehicle, request.SaveAsDraft, cancellationToken);
        var message = request.SaveAsDraft ? "Vehicle draft saved." : "Vehicle created successfully.";
        return ApiResponse<int>.SuccessResponse(id, message);
    }
}
