using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Queries;

public record GetCustomersQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<CustomerDto>>>;

public class GetCustomersQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetCustomersQuery, ApiResponse<PagedResult<CustomerDto>>>
{
    public async Task<ApiResponse<PagedResult<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var customers = await connection.QueryAsync<CustomerDto>(
            @"SELECT Id, FullName, Phone, Email, Address, CNIC, IsActive, CreatedAt
              FROM Customers WHERE IsDeleted = 0
              ORDER BY CreatedAt DESC
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { Offset = offset, request.PageSize });

        var totalCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Customers WHERE IsDeleted = 0");

        var result = new PagedResult<CustomerDto>
        {
            Items = customers.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<CustomerDto>>.SuccessResponse(result);
    }
}
