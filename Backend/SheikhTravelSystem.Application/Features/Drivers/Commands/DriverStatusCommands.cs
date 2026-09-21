using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Drivers.Commands;

public record ToggleDriverActiveCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "ToggleActive";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => Id;
}

public class ToggleDriverActiveCommandValidator : AbstractValidator<ToggleDriverActiveCommand>
{
    public ToggleDriverActiveCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class ToggleDriverActiveCommandHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<ToggleDriverActiveCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ToggleDriverActiveCommand request, CancellationToken cancellationToken)
    {
        var newValue = await driverRepository.ToggleActiveAsync(
            tenantContext.GetRequiredTenantId(), request.Id, cancellationToken);
        var label = newValue ? "active" : "inactive";
        return ApiResponse<bool>.SuccessResponse(newValue, $"Driver marked as {label}.");
    }
}

public record ChangeDriverStatusCommand(int Id, DriverStatus Status) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "ChangeStatus";
    public string AuditEntityName => "Driver";
    public int? AuditEntityId => Id;
}

public class ChangeDriverStatusCommandValidator : AbstractValidator<ChangeDriverStatusCommand>
{
    public ChangeDriverStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class ChangeDriverStatusCommandHandler(IDriverRepository driverRepository, ITenantContext tenantContext)
    : IRequestHandler<ChangeDriverStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangeDriverStatusCommand request, CancellationToken cancellationToken)
    {
        DriverAssignmentGuard.EnsureManualStatusAllowed(request.Status);
        await driverRepository.ChangeStatusAsync(
            tenantContext.GetRequiredTenantId(), request.Id, request.Status, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, $"Driver status updated to {request.Status}.");
    }
}
