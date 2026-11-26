namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ITenantRoleSeedService
{
    Task SeedSystemRolePermissionsAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Assigns all platform permissions to SUPER_ADMIN — intended only for the platform tenant (Id = 1).</summary>
    Task SeedSuperAdminPermissionsAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants the permissions of the given opt-in modules (see <see cref="ModulePermissionGrants"/>) to the tenant's
    /// administrator roles. Idempotent; modules without a grant are ignored.
    /// </summary>
    Task GrantModulePermissionsAsync(int tenantId, IReadOnlyCollection<string> moduleCodes, CancellationToken cancellationToken = default);
}
