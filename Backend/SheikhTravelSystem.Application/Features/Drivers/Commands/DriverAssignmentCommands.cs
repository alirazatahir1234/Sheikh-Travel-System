using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class UnassignDriverVehicleCommandHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<UnassignDriverVehicleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UnassignDriverVehicleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Driver", request.DriverId);

        var rows = await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE AssignmentHistory SET Status = N'Completed', EndAt = GETUTCDATE()
                  WHERE DriverId = @DriverId AND TenantId = @TenantId AND Status = N'Active' AND IsDeleted = 0",
                new { request.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

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

public class TransferDriverVehicleCommandHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    IMediator mediator)
    : IRequestHandler<TransferDriverVehicleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(TransferDriverVehicleCommand request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UnassignDriverVehicleCommand(request.DriverId), cancellationToken);
        return await mediator.Send(
            new AssignDriverVehicleCommand(request.DriverId, new AssignDriverVehicleRequest(
                request.Body.NewVehicleId,
                request.Body.BookingId,
                request.Body.AssignmentType ?? "Transfer")),
            cancellationToken);
    }
}
