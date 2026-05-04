using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Maintenance.Commands;

public record DeleteMaintenanceCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "Maintenance";
    public int? AuditEntityId => Id;
}

public class DeleteMaintenanceCommandValidator : AbstractValidator<DeleteMaintenanceCommand>
{
    public DeleteMaintenanceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public class DeleteMaintenanceCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteMaintenanceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteMaintenanceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Maintenance SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id, UpdatedAt = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        if (rowsAffected == 0)
            throw new NotFoundException("Maintenance", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, "Maintenance record deleted successfully.");
    }
}
