namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Phase 8 — trusted tenant for AI HTTP endpoints.
/// SuperAdmin may narrow via body <c>targetTenantId</c>; non–SuperAdmin forged overrides are rejected.
/// </summary>
public static class AiEffectiveTenantResolver
{
    public enum ResolveStatus
    {
        Ok,
        MissingTenantContext,
        ForgedTargetTenantRejected
    }

    public readonly record struct ResolveResult(
        ResolveStatus Status,
        int TenantId,
        string? Message);

    /// <summary>
    /// Resolve the effective tenant for AI assistant/chat/copilot.
    /// </summary>
    /// <param name="contextTenantId">Tenant from JWT / middleware tenant context.</param>
    /// <param name="isPlatformSuperAdmin">From JWT SuperAdmin role — never from the request body.</param>
    /// <param name="requestedTargetTenantId">Optional body override (SuperAdmin company-scope only).</param>
    public static ResolveResult Resolve(
        int? contextTenantId,
        bool isPlatformSuperAdmin,
        int? requestedTargetTenantId)
    {
        if (contextTenantId is not int ctx || ctx <= 0)
        {
            return new ResolveResult(
                ResolveStatus.MissingTenantContext,
                0,
                "Tenant context is required.");
        }

        if (requestedTargetTenantId is int target && target > 0 && target != ctx)
        {
            if (!isPlatformSuperAdmin)
            {
                return new ResolveResult(
                    ResolveStatus.ForgedTargetTenantRejected,
                    0,
                    "targetTenantId override is not allowed for this caller.");
            }

            return new ResolveResult(ResolveStatus.Ok, target, null);
        }

        return new ResolveResult(ResolveStatus.Ok, ctx, null);
    }
}
