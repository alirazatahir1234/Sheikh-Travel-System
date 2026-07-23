using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetUserByIdQuery(int Id) : IRequest<ApiResponse<UserDto>>;

public class GetUserByIdQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<GetUserByIdQuery, ApiResponse<UserDto>>
{
    public async Task<ApiResponse<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        UserQueries.UserRow? row;
        try
        {
            row = await connection.QuerySingleOrDefaultAsync<UserQueries.UserRow>(
                new CommandDefinition(
                    UserQueries.SelectSql + " WHERE u.Id = @Id AND u.IsDeleted = 0",
                    new { request.Id },
                    cancellationToken: cancellationToken));
        }
        catch
        {
            row = await connection.QuerySingleOrDefaultAsync<UserQueries.UserRow>(
                new CommandDefinition(
                    @"SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt,
                             TenantId AS CompanyId,
                             CAST(NULL AS NVARCHAR(200)) AS CompanyName,
                             BranchId, CAST(NULL AS NVARCHAR(200)) AS BranchName,
                             DepartmentId, CAST(NULL AS NVARCHAR(200)) AS DepartmentName
                      FROM Users WHERE Id = @Id AND IsDeleted = 0",
                    new { request.Id },
                    cancellationToken: cancellationToken));
        }

        if (row is null)
            throw new NotFoundException("User", request.Id);

        var tenantId = row.CompanyId ?? 0;
        if (tenantId > 0)
            platformScope.EnsureTenantAccess(tenantId);

        var roles = await UserRoleAssignment.LoadAssignedAsync(connection, request.Id, cancellationToken);
        return ApiResponse<UserDto>.SuccessResponse(
            UserQueries.ToDto(row, roles.Select(UserRoleAssignment.ToDto).ToList()));
    }
}
