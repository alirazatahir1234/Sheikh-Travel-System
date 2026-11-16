namespace SheikhTravelSystem.API.Authorization;

/// <summary>
/// Opt-in: platform DataRead callers without X-Tenant-Id may set
/// <c>ITenantContext.IsCrossTenantBypass</c> for intentional all-companies endpoints (SG-11).
/// Default middleware behaviour never sets bypass.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowCrossTenantBypassAttribute : Attribute;
