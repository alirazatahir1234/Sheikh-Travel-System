using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record ChangeVehicleStatusCommand(int Id, ChangeVehicleStatusRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "ChangeStatus";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class ChangeVehicleStatusCommandValidator : AbstractValidator<ChangeVehicleStatusCommand>
{
    public ChangeVehicleStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Body.Status).IsInEnum();
    }
}

public class ChangeVehicleStatusCommandHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<ChangeVehicleStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangeVehicleStatusCommand request, CancellationToken cancellationToken)
    {
        await vehicleRepository.ChangeStatusAsync(
            request.Id, tenantContext.GetRequiredTenantId(), request.Body.Status, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Vehicle status updated.");
    }
}

public record AssignVehicleDriverCommand(int Id, AssignVehicleDriverRequest Body)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "AssignDriver";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class AssignVehicleDriverCommandValidator : AbstractValidator<AssignVehicleDriverCommand>
{
    public AssignVehicleDriverCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Body.DriverId).GreaterThan(0);
    }
}

public class AssignVehicleDriverCommandHandler(
    IVehicleRepository vehicleRepository,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AssignVehicleDriverCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AssignVehicleDriverCommand request, CancellationToken cancellationToken)
    {
        var assignmentId = await vehicleRepository.AssignDriverAsync(
            request.Id, tenantContext.GetRequiredTenantId(), request.Body,
            currentUser.UserId?.ToString() ?? "api", cancellationToken);
        return ApiResponse<int>.SuccessResponse(assignmentId, "Driver assigned to vehicle.");
    }
}

public record AssignVehicleGpsCommand(int Id, AssignVehicleGpsRequest Body)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignGps";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class AssignVehicleGpsCommandValidator : AbstractValidator<AssignVehicleGpsCommand>
{
    public AssignVehicleGpsCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Body.GpsDeviceId).GreaterThan(0);
    }
}

public class AssignVehicleGpsCommandHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<AssignVehicleGpsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignVehicleGpsCommand request, CancellationToken cancellationToken)
    {
        await vehicleRepository.AssignGpsAsync(
            request.Id, tenantContext.GetRequiredTenantId(), request.Body.GpsDeviceId, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "GPS device assigned to vehicle.");
    }
}

public record PublishVehicleCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Publish";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class PublishVehicleCommandValidator : AbstractValidator<PublishVehicleCommand>
{
    public PublishVehicleCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class PublishVehicleCommandHandler(IVehicleRepository vehicleRepository, ITenantContext tenantContext)
    : IRequestHandler<PublishVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(PublishVehicleCommand request, CancellationToken cancellationToken)
    {
        await vehicleRepository.PublishAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Vehicle published successfully.");
    }
}
