namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ITenantModuleService
{
    Task<IReadOnlyList<string>> GetLegacyModuleKeysAsync(int tenantId, CancellationToken cancellationToken = default);
    Task SyncLegacyJsonAsync(int tenantId, IReadOnlyList<string> moduleCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the tenant has an explicit entitlement for <paramref name="moduleCode"/>
    /// (TenantModules row, or legacy EnabledModulesJson mapping). Defaults never invent premium modules.
    /// </summary>
    Task<bool> IsModuleEnabledAsync(int tenantId, string moduleCode, CancellationToken cancellationToken = default);
}
