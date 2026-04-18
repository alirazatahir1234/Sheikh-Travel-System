using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetUsersQuery(int Page = 1, int PageSize = 20) : IRequest<ApiResponse<PagedResult<UserDto>>>;

public class GetUsersQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetUsersQuery, ApiResponse<PagedResult<UserDto>>>
{
    public async Task<ApiResponse<PagedResult<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var users = await connection.QueryAsync<UserDto>(
            @"SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt
              FROM Users WHERE IsDeleted = 0
              ORDER BY CreatedAt DESC
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { Offset = offset, request.PageSize });

        var totalCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Users WHERE IsDeleted = 0");

        var result = new PagedResult<UserDto>
        {
            Items = users.ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return ApiResponse<PagedResult<UserDto>>.SuccessResponse(result);
    }
}
