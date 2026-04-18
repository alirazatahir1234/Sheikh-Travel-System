using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Maintenance.Commands;

public record CreateMaintenanceCommand(CreateMaintenanceDto Maintenance) : IRequest<ApiResponse<int>>;

public class CreateMaintenanceCommandValidator : AbstractValidator<CreateMaintenanceCommand>
{
    public CreateMaintenanceCommandValidator()
    {
        RuleFor(x => x.Maintenance.VehicleId).GreaterThan(0);
        RuleFor(x => x.Maintenance.Description).NotEmpty();
        RuleFor(x => x.Maintenance.Cost).GreaterThanOrEqualTo(0);
    }
}

public class CreateMaintenanceCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateMaintenanceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateMaintenanceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Maintenance;

        var id = await connection.ExecuteScalarAsync<int>(
            @"INSERT INTO Maintenance (VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
              Status, ServiceProvider, CreatedAt, IsDeleted)
              VALUES (@VehicleId, @Description, @Cost, @MaintenanceDate, @NextDueDate,
              @Status, @ServiceProvider, @CreatedAt, 0);
              SELECT SCOPE_IDENTITY();",
            new
            {
                dto.VehicleId, dto.Description, dto.Cost, dto.MaintenanceDate,
                dto.NextDueDate, Status = (int)MaintenanceStatus.Scheduled,
                dto.ServiceProvider, CreatedAt = DateTime.UtcNow
            });

        return ApiResponse<int>.SuccessResponse(id, "Maintenance record created successfully.");
    }
}
