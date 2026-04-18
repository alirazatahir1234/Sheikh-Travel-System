using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Application.Features.FuelLogs.Queries;

public record GetFuelLogsQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<FuelLogDto>>>;

public class GetFuelLogsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetFuelLogsQuery, ApiResponse<PagedResult<FuelLogDto>>>
{
    public async Task<ApiResponse<PagedResult<FuelLogDto>>> Handle(GetFuelLogsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var logs = await connection.QueryAsync<FuelLogDto>(
            @"SELECT Id, VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
              OdometerReading, FuelType, FuelDate, Station, CreatedAt
              FROM FuelLogs WHERE IsDeleted = 0
              ORDER BY FuelDate DESC
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { Offset = offset, request.PageSize });

        var totalCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM FuelLogs WHERE IsDeleted = 0");

        var result = new PagedResult<FuelLogDto>
        {
            Items = logs.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<FuelLogDto>>.SuccessResponse(result);
    }
}
