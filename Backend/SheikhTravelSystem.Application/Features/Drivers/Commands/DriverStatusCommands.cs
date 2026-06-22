using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class ToggleDriverActiveCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ToggleDriverActiveCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ToggleDriverActiveCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var current = await connection.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition(
                "SELECT IsActive FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (current is null)
            throw new NotFoundException("Driver", request.Id);

        var newValue = !current.Value;
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET IsActive = @IsActive, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId",
                new { IsActive = newValue, request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

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

public class ChangeDriverStatusCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<ChangeDriverStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangeDriverStatusCommand request, CancellationToken cancellationToken)
    {
        DriverAssignmentGuard.EnsureManualStatusAllowed(request.Status);

        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Drivers SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Status = (int)request.Status, request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (rows == 0)
            throw new NotFoundException("Driver", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, $"Driver status updated to {request.Status}.");
    }
}
