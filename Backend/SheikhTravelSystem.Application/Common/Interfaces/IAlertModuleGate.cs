namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Tenant entitlement gate for fleet alert types under the Alerts module.
/// Unknown / non-catalog event types are allowed (not invented gates).
/// </summary>
public interface IAlertModuleGate
{
    /// <summary>
    /// True when the tenant may raise/dispatch this GPS alert event type.
    /// Requires <c>ALERTS</c> plus the matching alert-type module (e.g. <c>ROAD_SPEED_ALERTS</c>).
    /// </summary>
    Task<bool> IsAlertTypeEnabledAsync(
        int tenantId,
        string eventType,
        CancellationToken cancellationToken = default);
}
