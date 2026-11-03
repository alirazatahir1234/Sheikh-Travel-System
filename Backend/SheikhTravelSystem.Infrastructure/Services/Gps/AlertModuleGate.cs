using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class AlertModuleGate(
    ITenantModuleService tenantModules,
    IPlatformRepository platformRepository) : IAlertModuleGate
{
    public async Task<bool> IsAlertTypeEnabledAsync(
        int tenantId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0 || string.IsNullOrWhiteSpace(eventType))
            return false;

        var typeDef = AlertModuleCatalog.FindByEventType(eventType);
        if (typeDef is null)
            return true; // Not in Alerts inventory — do not invent a gate

        if (!await tenantModules.IsModuleEnabledAsync(
                tenantId, AlertModuleCatalog.AlertsModuleCode, cancellationToken))
            return false;

        // GPS add-ons (geofencing / speed-alerts) → TenantFeatures.
        if (!string.IsNullOrWhiteSpace(typeDef.FeatureKey))
        {
            if (!await tenantModules.IsModuleEnabledAsync(
                    tenantId, TenantModuleCatalog.GpsModuleCode, cancellationToken))
                return false;

            var flags = await platformRepository.LoadTenantFeatureFlagsAsync(tenantId, cancellationToken);
            return flags.TryGetValue(typeDef.FeatureKey, out var enabled) && enabled;
        }

        return await tenantModules.IsModuleEnabledAsync(tenantId, typeDef.ModuleCode, cancellationToken);
    }
}
