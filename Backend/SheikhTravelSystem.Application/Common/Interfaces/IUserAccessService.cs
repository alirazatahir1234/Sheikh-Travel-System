namespace SheikhTravelSystem.Application.Common.Interfaces;

public record UserAccessContext(
    int UserId,
    int TenantId,
    IReadOnlyList<string> RoleCodes,
    IReadOnlyList<string> Permissions);

public interface IUserAccessService
{
    Task<UserAccessContext> ResolveAsync(int userId, int tenantId, CancellationToken cancellationToken = default);
}
