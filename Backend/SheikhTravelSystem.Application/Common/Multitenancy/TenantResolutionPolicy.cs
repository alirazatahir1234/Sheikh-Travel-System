namespace SheikhTravelSystem.Application.Common.Multitenancy;

/// <summary>Result of tenant resolution policy (pure; no I/O).</summary>
public sealed record TenantResolutionDecision(
    int? TenantId,
    string? Slug,
    bool Reject,
    string? RejectReason,
    string? WarnReason);

/// <summary>
/// Enforces JWT-authoritative tenant identity for authenticated users.
/// X-Tenant-Id must never allow a normal user to switch tenants.
/// </summary>
public static class TenantResolutionPolicy
{
    public const int AnonymousDefaultTenantId = 1;
    public const string AnonymousDefaultSlug = "default";

    public static TenantResolutionDecision Decide(
        int? jwtTenantId,
        int? headerTenantId,
        int? slugResolvedTenantId,
        string? slug,
        bool isAuthenticated,
        bool isPlatformOperator,
        bool allowAnonymousDefaultTenant)
    {
        if (isAuthenticated)
            return DecideAuthenticated(
                jwtTenantId,
                headerTenantId,
                slugResolvedTenantId,
                slug,
                isPlatformOperator);

        return DecideAnonymous(
            headerTenantId,
            slugResolvedTenantId,
            slug,
            allowAnonymousDefaultTenant);
    }

    private static TenantResolutionDecision DecideAuthenticated(
        int? jwtTenantId,
        int? headerTenantId,
        int? slugResolvedTenantId,
        string? slug,
        bool isPlatformOperator)
    {
        string? warn = null;

        if (headerTenantId.HasValue)
        {
            if (isPlatformOperator)
            {
                if (jwtTenantId.HasValue && headerTenantId.Value != jwtTenantId.Value)
                {
                    warn = "Platform operator overridden JWT tenant via X-Tenant-Id.";
                }

                warn = AppendSlugWarn(warn, jwtTenantId: headerTenantId, slugResolvedTenantId, slug);
                return new TenantResolutionDecision(
                    headerTenantId.Value,
                    PreferSlug(slug, headerTenantId),
                    Reject: false,
                    RejectReason: null,
                    WarnReason: warn);
            }

            if (!jwtTenantId.HasValue)
            {
                return new TenantResolutionDecision(
                    null,
                    slug,
                    Reject: true,
                    RejectReason: "Authenticated request is missing JWT tenant and cannot honor X-Tenant-Id.",
                    WarnReason: null);
            }

            if (headerTenantId.Value != jwtTenantId.Value)
            {
                return new TenantResolutionDecision(
                    null,
                    slug,
                    Reject: true,
                    RejectReason: "X-Tenant-Id does not match the authenticated tenant.",
                    WarnReason: null);
            }
        }

        if (!jwtTenantId.HasValue)
        {
            return new TenantResolutionDecision(
                null,
                slug,
                Reject: false,
                RejectReason: null,
                WarnReason: null);
        }

        warn = AppendSlugWarn(warn, jwtTenantId, slugResolvedTenantId, slug);
        return new TenantResolutionDecision(
            jwtTenantId.Value,
            PreferSlug(slug, jwtTenantId),
            Reject: false,
            RejectReason: null,
            WarnReason: warn);
    }

    private static TenantResolutionDecision DecideAnonymous(
        int? headerTenantId,
        int? slugResolvedTenantId,
        string? slug,
        bool allowAnonymousDefaultTenant)
    {
        // Anonymous must ignore X-Tenant-Id entirely.
        _ = headerTenantId;

        if (slugResolvedTenantId.HasValue)
        {
            return new TenantResolutionDecision(
                slugResolvedTenantId.Value,
                PreferSlug(slug, slugResolvedTenantId),
                Reject: false,
                RejectReason: null,
                WarnReason: null);
        }

        if (allowAnonymousDefaultTenant)
        {
            return new TenantResolutionDecision(
                AnonymousDefaultTenantId,
                string.IsNullOrWhiteSpace(slug) ? AnonymousDefaultSlug : slug,
                Reject: false,
                RejectReason: null,
                WarnReason: "Anonymous tenant resolution failed; applied transitional default tenant.");
        }

        return new TenantResolutionDecision(
            null,
            slug,
            Reject: false,
            RejectReason: null,
            WarnReason: "Anonymous tenant resolution failed; no default tenant applied.");
    }

    private static string? AppendSlugWarn(
        string? existing,
        int? jwtTenantId,
        int? slugResolvedTenantId,
        string? slug)
    {
        if (!slugResolvedTenantId.HasValue || !jwtTenantId.HasValue)
            return existing;
        if (slugResolvedTenantId.Value == jwtTenantId.Value)
            return existing;

        var msg = $"Tenant slug '{slug}' resolves to tenant {slugResolvedTenantId.Value} but JWT tenant {jwtTenantId.Value} is authoritative.";
        return string.IsNullOrEmpty(existing) ? msg : existing + " " + msg;
    }

    private static string? PreferSlug(string? slug, int? tenantId) =>
        !string.IsNullOrWhiteSpace(slug) ? slug : null;
}
