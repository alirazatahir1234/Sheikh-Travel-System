using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Queries;

public record GetCustomerListStatsQuery(
    string? Search = null,
    bool? IsActive = null
) : IRequest<ApiResponse<CustomerListStatsDto>>;

public class GetCustomerListStatsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetCustomerListStatsQuery, ApiResponse<CustomerListStatsDto>>
{
    public async Task<ApiResponse<CustomerListStatsDto>> Handle(GetCustomerListStatsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var (whereClause, parameters) = CustomerQueryFilters.Build(
            request.Search,
            request.IsActive,
            recency: null,
            applyRecency: false);

        var stats = await connection.QuerySingleAsync<CustomerListStatsDto>(
            new CommandDefinition(
                $@"SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN CreatedAt >= @NewSince THEN 1 ELSE 0 END) AS New,
                    SUM(CASE WHEN CreatedAt < @NewSince THEN 1 ELSE 0 END) AS Returning
                  FROM Customers
                  {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return ApiResponse<CustomerListStatsDto>.SuccessResponse(stats);
    }
}
