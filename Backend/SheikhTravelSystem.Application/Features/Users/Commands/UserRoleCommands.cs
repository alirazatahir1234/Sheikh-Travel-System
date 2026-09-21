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

public record SetUserRolesCommand(int UserId, SetUserRolesRequest Payload)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateRoles";
    public string AuditEntityName => "User";
    public int? AuditEntityId => UserId;
}

public class SetUserRolesCommandHandler(
    IUserRepository userRepository,
    IPlatformScope platformScope,
    ICurrentUserService currentUser) : IRequestHandler<SetUserRolesCommand, ApiResponse<bool>>
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
                UserRoleAssignment.EnsureCanAssignPlatformRole(code, currentUser);
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

        await userRepository.ReplaceRoleAssignmentsAsync(
            request.UserId,
            user.TenantId,
            roleIds,
            scopes,
            currentUser.UserId,
            cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "User roles updated.");
    }
}
