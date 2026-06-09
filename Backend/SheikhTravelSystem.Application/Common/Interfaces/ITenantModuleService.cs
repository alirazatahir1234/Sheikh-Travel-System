namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ITenantModuleService
{
    Task<IReadOnlyList<string>> GetLegacyModuleKeysAsync(int tenantId, CancellationToken cancellationToken = default);
    Task SyncLegacyJsonAsync(int tenantId, IReadOnlyList<string> moduleCodes, CancellationToken cancellationToken = default);
}
