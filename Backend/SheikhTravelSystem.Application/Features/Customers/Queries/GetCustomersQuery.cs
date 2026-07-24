using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.Customers.Queries;

public record GetCustomersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null,
    string? Recency = null
) : IRequest<ApiResponse<PagedResult<CustomerDto>>>;

public class GetCustomersQueryHandler(
    IDbConnectionFactory dbFactory,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IDataScopeEngine dataScopeEngine)
    : IRequestHandler<GetCustomersQuery, ApiResponse<PagedResult<CustomerDto>>>
{
    public async Task<ApiResponse<PagedResult<CustomerDto>>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var tenantId = tenantContext.GetRequiredTenantId();
        var (whereClause, parameters) = CustomerQueryFilters.Build(
            request.Search,
            request.IsActive,
            request.Recency,
            tenantId: tenantId);

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", request.PageSize);

        // Customers have no BranchId/DepartmentId — tenant isolation + soft company-wide/pass-through only.
        if (currentUser.UserId is int userId)
            _ = await dataScopeEngine.ResolveAsync(userId, tenantId, cancellationToken);

        var customers = await connection.QueryAsync<CustomerDto>(
            new CommandDefinition(
                $@"SELECT Id, FullName, Phone, Email, Address, CNIC, IsActive, CreatedAt,
                  FatherOrHusbandName, Gender, DateOfBirth, Nationality
                  FROM Customers
                  {whereClause}
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $"SELECT COUNT(*) FROM Customers {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

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
