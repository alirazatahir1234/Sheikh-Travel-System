using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;

namespace SheikhTravelSystem.Application.Features.Vehicles.Queries;

public record GetVehiclesQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<VehicleDto>>>;

public class GetVehiclesQueryHandler(IDbConnectionFactory dbFactory, ITenantContext tenantContext)
    : IRequestHandler<GetVehiclesQuery, ApiResponse<PagedResult<VehicleDto>>>
{
    public async Task<ApiResponse<PagedResult<VehicleDto>>> Handle(GetVehiclesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var tenantId = tenantContext.GetRequiredTenantId();

        var vehicles = await connection.QueryAsync<VehicleDto>(
            new CommandDefinition(
                @"SELECT Id, Name, RegistrationNumber, Model, Year, SeatingCapacity, FuelAverage,
                  FuelType, CurrentMileage, InsuranceExpiryDate, Status, CreatedAt
                  FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, request.PageSize, TenantId = tenantId },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Vehicles WHERE IsDeleted = 0 AND TenantId = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

        var result = new PagedResult<VehicleDto>
        {
            Items = vehicles.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<VehicleDto>>.SuccessResponse(result);
    }
}
