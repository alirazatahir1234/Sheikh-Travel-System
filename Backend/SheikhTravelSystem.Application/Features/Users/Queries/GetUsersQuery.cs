using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetUsersQuery(int Page = 1, int PageSize = 20, int? TenantId = null)
    : IRequest<ApiResponse<PagedResult<UserDto>>>;

public class GetUsersQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<GetUsersQuery, ApiResponse<PagedResult<UserDto>>>
{
    public async Task<ApiResponse<PagedResult<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var tenantId = ResolveTenantFilter(request.TenantId);

        var users = await connection.QueryAsync<UserDto>(
            new CommandDefinition(
                @"SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt
                  FROM Users WHERE IsDeleted = 0 AND TenantId = @TenantId
                  ORDER BY CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { TenantId = tenantId, Offset = offset, request.PageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Users WHERE IsDeleted = 0 AND TenantId = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<UserDto>>.SuccessResponse(new PagedResult<UserDto>
        {
            Items = users.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    private int ResolveTenantFilter(int? requestedTenantId)
    {
        if (requestedTenantId.HasValue)
        {
            platformScope.EnsureTenantAccess(requestedTenantId.Value);
            return requestedTenantId.Value;
        }

        return platformScope.TenantId;
    }
}
