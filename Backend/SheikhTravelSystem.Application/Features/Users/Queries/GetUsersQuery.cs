using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Queries;

public record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    int? TenantId = null,
    int? BranchId = null,
    int? DepartmentId = null,
    string? Status = null,
    string? EmployeeType = null,
    string? Search = null)
    : IRequest<ApiResponse<PagedResult<UserDto>>>;

public class GetUsersQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<GetUsersQuery, ApiResponse<PagedResult<UserDto>>>
{
    public async Task<ApiResponse<PagedResult<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        int tenantId;
        try
        {
            tenantId = ResolveTenantFilter(request.TenantId);
        }
        catch (InvalidOperationException)
        {
            throw new ForbiddenException(
                "Company context is missing. Sign in again or select a company (tenant) before loading users.");
        }

        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var where = """
            WHERE u.IsDeleted = 0 AND u.TenantId = @TenantId
            """;
        if (request.BranchId.HasValue)
            where += " AND u.BranchId = @BranchId";
        if (request.DepartmentId.HasValue)
            where += " AND u.DepartmentId = @DepartmentId";
        if (!string.IsNullOrWhiteSpace(request.Status))
            where += " AND COALESCE(u.Status, CASE WHEN u.IsActive = 1 THEN N'Active' ELSE N'Inactive' END) = @Status";
        if (!string.IsNullOrWhiteSpace(request.EmployeeType))
            where += " AND u.EmployeeType = @EmployeeType";
        if (!string.IsNullOrWhiteSpace(request.Search))
            where += """
                 AND (
                    u.FullName LIKE @Search OR u.Email LIKE @Search OR u.Phone LIKE @Search
                    OR u.EmployeeCode LIKE @Search OR u.JobTitle LIKE @Search
                 )
                """;

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search.Trim()}%";
        var args = new
        {
            TenantId = tenantId,
            request.BranchId,
            request.DepartmentId,
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : UserLifecycle.Normalize(request.Status),
            EmployeeType = EmployeeTypes.Normalize(request.EmployeeType),
            Search = search,
            Offset = offset,
            request.PageSize
        };

        try
        {
            var enrichedSql = UserQueries.SelectSql + "\n" + where + """

                        ORDER BY u.CreatedAt DESC
                        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                        """;
            var rows = (await connection.QueryAsync<UserQueries.UserRow>(
                new CommandDefinition(
                    enrichedSql,
                    args,
                    cancellationToken: cancellationToken))).ToList();

            var totalCount = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM Users u\n" + where,
                    args,
                    cancellationToken: cancellationToken));

            var roleMap = await UserQueries.LoadAssignedRolesMapAsync(
                connection, rows.Select(r => r.Id).ToList(), cancellationToken);

            return ApiResponse<PagedResult<UserDto>>.SuccessResponse(new PagedResult<UserDto>
            {
                Items = rows.Select(r => UserQueries.ToDto(
                    r, roleMap.TryGetValue(r.Id, out var roles) ? roles : null)).ToList(),
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
        }
        catch (Exception)
        {
            // Columns / joins may be incomplete — fall back to legacy select via row type
            // (Dapper cannot materialize UserDto records with optional ctor params).
            var rows = (await connection.QueryAsync<UserQueries.UserRow>(
                new CommandDefinition(
                    """
                    SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt,
                           TenantId AS CompanyId,
                           CAST(NULL AS nvarchar(200)) AS CompanyName,
                           CAST(NULL AS int) AS BranchId,
                           CAST(NULL AS nvarchar(200)) AS BranchName,
                           CAST(NULL AS int) AS DepartmentId,
                           CAST(NULL AS nvarchar(200)) AS DepartmentName,
                           CAST(NULL AS nvarchar(200)) AS JobTitle,
                           CAST(NULL AS nvarchar(50)) AS EmployeeCode,
                           CAST(NULL AS nvarchar(50)) AS EmployeeType,
                           CASE WHEN IsActive = 1 THEN N'Active' ELSE N'Inactive' END AS Status,
                           CAST(NULL AS nvarchar(100)) AS DefaultWorkspaceKey,
                           CAST(NULL AS nvarchar(100)) AS DefaultDashboardKey,
                           CAST(NULL AS nvarchar(200)) AS HomeRoute,
                           CAST(NULL AS nvarchar(100)) AS TimeZone,
                           CAST(NULL AS nvarchar(20)) AS Language,
                           CAST(NULL AS nvarchar(50)) AS Theme,
                           CAST(NULL AS nvarchar(500)) AS AvatarUrl,
                           CAST(NULL AS datetime2) AS LastLoginAt
                    FROM Users
                    WHERE IsDeleted = 0 AND TenantId = @TenantId
                    ORDER BY CreatedAt DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                    """,
                    new { TenantId = tenantId, Offset = offset, request.PageSize },
                    cancellationToken: cancellationToken))).ToList();

            var totalCount = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM Users WHERE IsDeleted = 0 AND TenantId = @TenantId",
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken));

            return ApiResponse<PagedResult<UserDto>>.SuccessResponse(new PagedResult<UserDto>
            {
                Items = rows.Select(r => UserQueries.ToDto(r)).ToList(),
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
        }
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
