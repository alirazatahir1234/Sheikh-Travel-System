using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Queries;

public record GetDriverAllowanceRulesQuery(int Page = 1, int PageSize = 50, bool ActiveOnly = false)
    : IRequest<ApiResponse<PagedResult<DriverAllowanceRuleDto>>>;

public class GetDriverAllowanceRulesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetDriverAllowanceRulesQuery, ApiResponse<PagedResult<DriverAllowanceRuleDto>>>
{
    public async Task<ApiResponse<PagedResult<DriverAllowanceRuleDto>>> Handle(
        GetDriverAllowanceRulesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var filter = request.ActiveOnly ? "AND IsActive = 1" : string.Empty;

        var rules = await connection.QueryAsync<DriverAllowanceRuleDto>(
            new CommandDefinition(
                $@"SELECT Id, Name, CalculationType, Value, Priority,
                          MinDistanceKm, MaxDistanceKm, VehicleFuelType, RouteFilter,
                          IsActive, Notes, CreatedAt
                   FROM DriverAllowanceRules
                   WHERE IsDeleted = 0 {filter}
                   ORDER BY Priority ASC, Id ASC
                   OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM DriverAllowanceRules WHERE IsDeleted = 0 {filter}",
                cancellationToken: cancellationToken));

        var result = new PagedResult<DriverAllowanceRuleDto>
        {
            Items = rules.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<DriverAllowanceRuleDto>>.SuccessResponse(result);
    }
}
