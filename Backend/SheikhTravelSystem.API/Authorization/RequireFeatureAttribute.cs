using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.API.Authorization;

/// <summary>
/// Requires the current tenant to have the given <c>TenantFeatures</c> add-on enabled (e.g. <c>geofencing</c>).
/// Responds 403 with <c>code = "feature_disabled"</c> and <c>featureKey</c> so apps can show an upsell state.
/// Additive to <see cref="RequirePermissionAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireFeatureAttribute(string featureKey, string displayName) : Attribute, IAsyncAuthorizationFilter
{
    public string FeatureKey { get; } = featureKey;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        if (services.GetService(typeof(ITenantContext)) is not ITenantContext tenantContext
            || services.GetService(typeof(IPlatformRepository)) is not IPlatformRepository platformRepository)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var tenantId = tenantContext.TenantId ?? 0;
        if (tenantId > 0)
        {
            var flags = await platformRepository.LoadTenantFeatureFlagsAsync(tenantId, context.HttpContext.RequestAborted);
            if (flags.TryGetValue(FeatureKey, out var enabled) && enabled)
                return;
        }

        context.Result = new ObjectResult(new
        {
            success = false,
            code = "feature_disabled",
            featureKey = FeatureKey,
            message = $"{displayName} is not enabled for this company. Ask your administrator to enable the {displayName} add-on."
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
