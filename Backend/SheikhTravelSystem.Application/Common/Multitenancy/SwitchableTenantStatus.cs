namespace SheikhTravelSystem.Application.Common.Multitenancy;

/// <summary>
/// Console switcher status for SG-11 (includes suspended / expired / deactivated).
/// </summary>
public static class SwitchableTenantStatus
{
    public const string Active = "Active";
    public const string Suspended = "Suspended";
    public const string Expired = "Expired";
    public const string Deactivated = "Deactivated";

    /// <summary>
    /// Priority: Suspended → Expired → Deactivated (!IsActive) → Active.
    /// PastDue/Cancelled without IsActive=0 remain selectable as Active for renew/inspect.
    /// </summary>
    public static string Resolve(bool isActive, string? tenantStatus, string? subscriptionStatus)
    {
        if (Eq(tenantStatus, Suspended) || Eq(subscriptionStatus, Suspended))
            return Suspended;

        if (Eq(tenantStatus, Expired) || Eq(subscriptionStatus, Expired))
            return Expired;

        if (!isActive)
            return Deactivated;

        return Active;
    }

    private static bool Eq(string? value, string expected) =>
        string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
}
