using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;

namespace SheikhTravelSystem.Application.Features.Drivers.Queries;

public record GetDriversQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<DriverDto>>>;

public class GetDriversQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDriversQuery, ApiResponse<PagedResult<DriverDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverDto>>> Handle(GetDriversQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var drivers = await connection.QueryAsync<DriverDto>(
            @"SELECT Id, FullName, Phone, LicenseNumber, LicenseExpiryDate, CNIC, Address, Status, IsActive, CreatedAt
              FROM Drivers WHERE IsDeleted = 0
              ORDER BY CreatedAt DESC
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { Offset = offset, request.PageSize });

        var totalCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Drivers WHERE IsDeleted = 0");

        var result = new PagedResult<DriverDto>
        {
            Items = drivers.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<DriverDto>>.SuccessResponse(result);
    }
}
