using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Maintenance.Commands;

public record UpdateMaintenanceStatusCommand(int Id, MaintenanceStatus Status) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Maintenance";
    public int? AuditEntityId => Id;
}

public class UpdateMaintenanceStatusCommandValidator : AbstractValidator<UpdateMaintenanceStatusCommand>
{
    public UpdateMaintenanceStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class UpdateMaintenanceStatusCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateMaintenanceStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMaintenanceStatusCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Maintenance WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Maintenance", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Maintenance SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { Status = (int)request.Status, UpdatedAt = DateTime.UtcNow, request.Id },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Maintenance status updated successfully.");
    }
}
