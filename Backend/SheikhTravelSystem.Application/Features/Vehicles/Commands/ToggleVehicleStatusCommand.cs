using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Vehicles.Commands;

public record ToggleVehicleStatusCommand(int Id) : IRequest<ApiResponse<VehicleStatus>>, IAuditableCommand
{
    public string AuditAction => "ToggleStatus";
    public string AuditEntityName => "Vehicle";
    public int? AuditEntityId => Id;
}

public class ToggleVehicleStatusCommandValidator : AbstractValidator<ToggleVehicleStatusCommand>
{
    public ToggleVehicleStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class ToggleVehicleStatusCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<ToggleVehicleStatusCommand, ApiResponse<VehicleStatus>>
{
    public async Task<ApiResponse<VehicleStatus>> Handle(ToggleVehicleStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var currentStatus = await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (currentStatus == null)
            throw new NotFoundException("Vehicle", request.Id);

        var newStatus = (VehicleStatus)currentStatus == VehicleStatus.Available
            ? VehicleStatus.Retired
            : VehicleStatus.Available;

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { request.Id, Status = (int)newStatus, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        return ApiResponse<VehicleStatus>.SuccessResponse(newStatus, $"Vehicle status changed to {newStatus}.");
    }
}
