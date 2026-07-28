using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record GetUserRolesQuery(int UserId) : IRequest<ApiResponse<IReadOnlyList<AssignedRoleDto>>>;

public class GetUserRolesQueryHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope) : IRequestHandler<GetUserRolesQuery, ApiResponse<IReadOnlyList<AssignedRoleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AssignedRoleDto>>> Handle(
        GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var tenantId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TenantId FROM Users WHERE Id = @Id AND IsDeleted = 0",
            new { Id = request.UserId }, cancellationToken: cancellationToken));
        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.UserId);

        platformScope.EnsureTenantAccess(tenantId.Value);
        var rows = await UserRoleAssignment.LoadAssignedAsync(connection, request.UserId, cancellationToken);
        return ApiResponse<IReadOnlyList<AssignedRoleDto>>.SuccessResponse(
            rows.Select(UserRoleAssignment.ToDto).ToList());
    }
}

public record SetUserRolesCommand(int UserId, SetUserRolesRequest Payload)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateRoles";
    public string AuditEntityName => "User";
    public int? AuditEntityId => UserId;
}

public class SetUserRolesCommandHandler(
    IDbConnectionFactory dbFactory,
    IPlatformScope platformScope,
    ICurrentUserService currentUser) : IRequestHandler<SetUserRolesCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SetUserRolesCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var user = await connection.QuerySingleOrDefaultAsync<(int? TenantId, int? BranchId, int? DepartmentId)>(
            new CommandDefinition(
                "SELECT TenantId, BranchId, DepartmentId FROM Users WHERE Id = @Id AND IsDeleted = 0",
                new { Id = request.UserId }, cancellationToken: cancellationToken));
        if (user.TenantId is not int tenantId)
            throw new NotFoundException("User", request.UserId);

        platformScope.EnsureTenantAccess(tenantId);

        var roleIds = request.Payload.RoleIds ?? Array.Empty<int>();
        if (roleIds.Count > 0)
        {
            var assignedCodes = (await connection.QueryAsync<string>(new CommandDefinition(
                """
                SELECT Code FROM Roles
                WHERE TenantId = @TenantId AND Id IN @RoleIds AND IsActive = 1
                """,
                new { TenantId = tenantId, RoleIds = roleIds.Distinct().ToList() },
                cancellationToken: cancellationToken))).ToList();

            if (assignedCodes.Any(c => string.Equals(c, PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                && !currentUser.IsPlatformSuperAdmin)
            {
                throw new ForbiddenException("Only platform owners can assign the Super Admin role.");
            }
        }

        var scopes = new Dictionary<int, (int? BranchId, int? DepartmentId)>();
        foreach (var scope in request.Payload.Scopes ?? Array.Empty<RoleAssignmentScopeDto>())
        {
            var branchId = scope.BranchId ?? user.BranchId;
            var departmentId = scope.DepartmentId ?? user.DepartmentId;
            scopes[scope.RoleId] = (branchId, departmentId);
        }

        foreach (var roleId in roleIds.Where(id => !scopes.ContainsKey(id)))
            scopes[roleId] = (user.BranchId, user.DepartmentId);

        foreach (var scope in scopes.Values)
        {
            await UserQueries.EnsureOrgBelongsToTenantAsync(
                connection, tenantId, scope.BranchId, scope.DepartmentId, cancellationToken);
        }

        await UserRoleAssignment.ReplaceAssignmentsAsync(
            connection,
            request.UserId,
            tenantId,
            roleIds,
            scopes,
            currentUser.UserId,
            cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "User roles updated.");
    }
}
