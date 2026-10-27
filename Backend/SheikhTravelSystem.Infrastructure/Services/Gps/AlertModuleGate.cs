using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Gps;

public sealed class AlertModuleGate(ITenantModuleService tenantModules) : IAlertModuleGate
{
    public async Task<bool> IsAlertTypeEnabledAsync(
        int tenantId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0 || string.IsNullOrWhiteSpace(eventType))
            return false;

        var typeModule = AlertModuleCatalog.ModuleCodeForEventType(eventType);
        if (typeModule is null)
            return true; // Not in Alerts inventory — do not invent a gate

        if (!await tenantModules.IsModuleEnabledAsync(
                tenantId, AlertModuleCatalog.AlertsModuleCode, cancellationToken))
            return false;

        return await tenantModules.IsModuleEnabledAsync(tenantId, typeModule, cancellationToken);
    }
}
