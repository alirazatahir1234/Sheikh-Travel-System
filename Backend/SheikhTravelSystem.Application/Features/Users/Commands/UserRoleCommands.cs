using System.Text.Json;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;

namespace SheikhTravelSystem.Application.Features.Users.Commands;

public record GetUserRolesQuery(int UserId) : IRequest<ApiResponse<IReadOnlyList<AssignedRoleDto>>>;

public class GetUserRolesQueryHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope) : IRequestHandler<GetUserRolesQuery, ApiResponse<IReadOnlyList<AssignedRoleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<AssignedRoleDto>>> Handle(
        GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = await userRepository.GetTenantIdAsync(request.UserId, cancellationToken);
        if (!tenantId.HasValue)
            throw new NotFoundException("User", request.UserId);

        platformScope.EnsureTenantAccess(tenantId.Value);
        var roles = await userRepository.GetAssignedRolesAsync(request.UserId, cancellationToken);
        return ApiResponse<IReadOnlyList<AssignedRoleDto>>.SuccessResponse(roles);
    }
}

/// <summary>
/// Role assignment is an explicit fail-closed security event (SG-07) — not IAuditableCommand.
/// </summary>
public record SetUserRolesCommand(int UserId, SetUserRolesRequest Payload)
    : IRequest<ApiResponse<bool>>;

public class SetUserRolesCommandHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope,
    ICurrentUserService currentUser,
    IDbConnectionFactory dbFactory,
    IAuditEngine auditEngine) : IRequestHandler<SetUserRolesCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SetUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetOrgInfoAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new NotFoundException("User", request.UserId);

        platformScope.EnsureTenantAccess(user.TenantId);

        var roleIds = request.Payload.RoleIds ?? Array.Empty<int>();
        if (roleIds.Count > 0)
        {
            var assignedCodes = await userRepository.GetActiveRoleCodesAsync(
                user.TenantId, roleIds.Distinct().ToList(), cancellationToken);

            foreach (var code in assignedCodes)
                UserRoleAssignment.EnsureCanAssignPlatformRole(code, currentUser, user.TenantId);
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
            await userRepository.EnsureOrgBelongsToTenantAsync(
                user.TenantId, scope.BranchId, scope.DepartmentId, cancellationToken);
        }

        var previous = await userRepository.GetAssignedRolesAsync(request.UserId, cancellationToken);
        var oldRoleIds = previous.Select(r => r.RoleId).OrderBy(x => x).ToList();
        var newRoleIds = roleIds.Distinct().OrderBy(x => x).ToList();

        using var connection = dbFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            await userRepository.ReplaceRoleAssignmentsAsync(
                connection,
                tx,
                request.UserId,
                user.TenantId,
                roleIds,
                scopes,
                currentUser.UserId,
                cancellationToken);

            await auditEngine.RecordAsync(new AuditEventWrite(
                EventKey: AuditEventKeys.RoleAssignment,
                EntityType: "User",
                EntityId: request.UserId,
                Action: "UpdateRoles",
                OldValues: JsonSerializer.Serialize(new { roleIds = oldRoleIds }),
                NewValues: JsonSerializer.Serialize(new
                {
                    roleIds = newRoleIds,
                    added = newRoleIds.Except(oldRoleIds).ToList(),
                    removed = oldRoleIds.Except(newRoleIds).ToList()
                }),
                Success: true,
                Message: $"User {request.UserId} roles updated",
                TenantId: user.TenantId,
                TargetTenantId: user.TenantId,
                FailPolicy: AuditFailPolicy.FailClosed), connection, tx, cancellationToken);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return ApiResponse<bool>.SuccessResponse(true, "User roles updated.");
    }
}
