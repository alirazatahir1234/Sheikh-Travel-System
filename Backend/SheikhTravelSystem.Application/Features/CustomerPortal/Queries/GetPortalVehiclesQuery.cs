using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

public record GetPortalVehiclesQuery : IRequest<ApiResponse<IReadOnlyList<PortalVehicleDto>>>;

public class GetPortalVehiclesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPortalVehiclesQuery, ApiResponse<IReadOnlyList<PortalVehicleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<PortalVehicleDto>>> Handle(GetPortalVehiclesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<PortalVehicleDto>(
            new CommandDefinition(
                @"SELECT Id,
                         Name,
                         RegistrationNumber,
                         SeatingCapacity,
                         FuelAverage,
                         Model,
                         [Year],
                         FuelType,
                         Status
                  FROM Vehicles
                  WHERE IsDeleted = 0 AND Status <> @Retired
                  ORDER BY Name",
                new { Retired = (int)VehicleStatus.Retired },
                cancellationToken: cancellationToken));

        return ApiResponse<IReadOnlyList<PortalVehicleDto>>.SuccessResponse(rows.AsList(), "Vehicles loaded.");
    }
}
