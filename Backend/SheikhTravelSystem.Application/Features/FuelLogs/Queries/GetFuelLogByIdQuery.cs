using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.FuelLogs.Queries;

public record GetFuelLogByIdQuery(int Id) : IRequest<ApiResponse<FuelLogDto>>;

public class GetFuelLogByIdQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetFuelLogByIdQuery, ApiResponse<FuelLogDto>>
{
    public async Task<ApiResponse<FuelLogDto>> Handle(GetFuelLogByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var fuelLog = await connection.QuerySingleOrDefaultAsync<FuelLogDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, CreatedAt
                  FROM FuelLogs WHERE Id = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (fuelLog == null)
            throw new NotFoundException("FuelLog", request.Id);

        return ApiResponse<FuelLogDto>.SuccessResponse(fuelLog);
    }
}
