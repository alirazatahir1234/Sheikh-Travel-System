using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record GetVehicleByIdQuery(int Id) : IRequest<ApiResponse<VehicleDto>>;

public class GetVehicleByIdQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehicleByIdQuery, ApiResponse<VehicleDto>>
{
    public async Task<ApiResponse<VehicleDto>> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicle = await connection.QuerySingleOrDefaultAsync<VehicleDto>(
            new CommandDefinition(
                $@"SELECT {VehicleSql.DetailColumns}
                  FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { request.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (vehicle is null)
            throw new NotFoundException("Vehicle", request.Id);

        return ApiResponse<VehicleDto>.SuccessResponse(vehicle);
    }
}
