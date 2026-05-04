using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;

namespace SheikhTravelSystem.Application.Features.Maintenance.Commands;

public record UpdateMaintenanceCommand(int Id, CreateMaintenanceDto Maintenance) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Maintenance";
    public int? AuditEntityId => Id;
}

public class UpdateMaintenanceCommandValidator : AbstractValidator<UpdateMaintenanceCommand>
{
    public UpdateMaintenanceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Maintenance.VehicleId).GreaterThan(0);
        RuleFor(x => x.Maintenance.Description).NotEmpty();
        RuleFor(x => x.Maintenance.Cost).GreaterThanOrEqualTo(0);
    }
}

public class UpdateMaintenanceCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateMaintenanceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateMaintenanceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Maintenance;

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Maintenance 
                  SET VehicleId = @VehicleId, Description = @Description, Cost = @Cost,
                      MaintenanceDate = @MaintenanceDate, NextDueDate = @NextDueDate,
                      ServiceProvider = @ServiceProvider, UpdatedAt = @UpdatedAt
                  WHERE Id = @Id AND IsDeleted = 0",
                new
                {
                    request.Id,
                    dto.VehicleId, dto.Description, dto.Cost, dto.MaintenanceDate,
                    dto.NextDueDate, dto.ServiceProvider, UpdatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        if (rowsAffected == 0)
            throw new NotFoundException("Maintenance", request.Id);

        return ApiResponse<bool>.SuccessResponse(true, "Maintenance record updated successfully.");
    }
}
