using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record UnassignDriverVehicleCommand(int DriverId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UnassignVehicle";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class UnassignDriverVehicleCommandValidator : AbstractValidator<UnassignDriverVehicleCommand>
{
    public UnassignDriverVehicleCommandValidator() => RuleFor(x => x.DriverId).GreaterThan(0);
}

public class UnassignDriverVehicleCommandHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<UnassignDriverVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UnassignDriverVehicleCommand request, CancellationToken cancellationToken)
    {
        var rows = await driverRepository.UnassignVehicleAsync(
            tenantContext.GetRequiredTenantId(), request.DriverId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, rows > 0 ? "Vehicle assignment removed." : "No active assignment to remove.");
    }
}

public record TransferDriverVehicleCommand(int DriverId, TransferDriverVehicleRequest Body)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "TransferVehicle";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => DriverId;
}

public class TransferDriverVehicleCommandValidator : AbstractValidator<TransferDriverVehicleCommand>
{
    public TransferDriverVehicleCommandValidator()
    {
        RuleFor(x => x.DriverId).GreaterThan(0);
        RuleFor(x => x.Body.NewVehicleId).GreaterThan(0);
    }
}

public class TransferDriverVehicleCommandHandler(IMediator mediator)
    : IRequestHandler<TransferDriverVehicleCommand, ApiResponse<int>>
{
    public Task<ApiResponse<int>> Handle(TransferDriverVehicleCommand request, CancellationToken cancellationToken)
        => mediator.Send(
            new AssignDriverVehicleCommand(request.DriverId, new AssignDriverVehicleRequest(
                request.Body.NewVehicleId,
                request.Body.BookingId,
                request.Body.AssignmentType ?? "Transfer",
                request.Body.Remarks,
                request.Body.EffectiveFrom,
                request.Body.EffectiveTo)),
            cancellationToken);
}
