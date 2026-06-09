namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ITenantRoleSeedService
{
    Task SeedSystemRolePermissionsAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Assigns all platform permissions to SUPER_ADMIN — intended only for the platform tenant (Id = 1).</summary>
    Task SeedSuperAdminPermissionsAsync(int tenantId, CancellationToken cancellationToken = default);
}
