using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Authentication;

public class UserAccessService(IPermissionEngine permissionEngine) : IUserAccessService
{
    public async Task<UserAccessContext> ResolveAsync(
        int userId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        var result = await permissionEngine.EvaluateAsync(userId, tenantId, cancellationToken);
        return new UserAccessContext(
            result.UserId,
            result.TenantId,
            result.RoleCodes,
            result.PermissionCodes);
    }
}
