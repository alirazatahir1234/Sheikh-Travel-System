using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;

namespace SheikhTravelSystem.Application.Features.Maintenance.Queries;

public record GetMaintenanceByIdQuery(int Id) : IRequest<ApiResponse<MaintenanceDto>>;

public class GetMaintenanceByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetMaintenanceByIdQuery, ApiResponse<MaintenanceDto>>
{
    public async Task<ApiResponse<MaintenanceDto>> Handle(GetMaintenanceByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var maintenance = await connection.QuerySingleOrDefaultAsync<MaintenanceDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
                  Status, ServiceProvider, CreatedAt
                  FROM Maintenance WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (maintenance == null)
            throw new NotFoundException("Maintenance", request.Id);

        return ApiResponse<MaintenanceDto>.SuccessResponse(maintenance);
    }
}
