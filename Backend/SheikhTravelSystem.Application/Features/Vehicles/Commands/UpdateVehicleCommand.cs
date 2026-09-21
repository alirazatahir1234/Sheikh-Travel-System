using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Domain.Enums;
using static SheikhTravelSystem.Application.Features.Vehicles.VehicleDescriptiveTextRules;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record UpdateVehicleCommand(int Id, UpdateVehicleDto Vehicle) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Vehicle.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Vehicle.RegistrationNumber)
            .NotEmpty()
            .MaximumLength(20)
            .When(x => x.Vehicle.Status != VehicleStatus.Draft);
        RuleFor(x => x.Vehicle.RegistrationNumber)
            .MaximumLength(20)
            .When(x => x.Vehicle.Status == VehicleStatus.Draft);
        RuleFor(x => x.Vehicle.FuelAverage).GreaterThan(0);
        RuleFor(x => x.Vehicle.SeatingCapacity).GreaterThan(0);

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

public class UpdateVehicleCommandHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<UpdateVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        await vehicleRepository.UpdateAsync(
            request.Id, tenantContext.GetRequiredTenantId(), request.Vehicle, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Vehicle updated successfully.");
    }
}
