using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.API.Authorization;

/// <summary>
/// Requires the current tenant to have the given product module enabled
/// (<see cref="ITenantModuleService.IsModuleEnabledAsync"/>). Use with
/// <see cref="RequirePermissionAttribute"/> — module entitlement is additive to RBAC.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireModuleAttribute(string moduleCode) : Attribute, IAsyncAuthorizationFilter
{
    public string ModuleCode { get; } = moduleCode;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var tenantContext = context.HttpContext.RequestServices.GetService(typeof(ITenantContext))
            as ITenantContext;
        var tenantModules = context.HttpContext.RequestServices.GetService(typeof(ITenantModuleService))
            as ITenantModuleService;

        if (tenantContext is null || tenantModules is null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        var tenantId = tenantContext.TenantId ?? 0;
        if (tenantId <= 0)
        {
            context.Result = new ObjectResult(new
            {
                message = "Tenant context is required for module entitlement."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        var enabled = await tenantModules.IsModuleEnabledAsync(
            tenantId, ModuleCode, context.HttpContext.RequestAborted);

        if (enabled)
            return;

        var platformRepository = context.HttpContext.RequestServices.GetService(typeof(IPlatformRepository))
            as IPlatformRepository;
        if (platformRepository is not null
            && await platformRepository.AreSubscriptionFeaturesLockedAsync(tenantId, context.HttpContext.RequestAborted)
            && !SubscriptionBillingLifecycle.IsModuleAllowedWhenLocked(ModuleCode))
        {
            context.Result = new ObjectResult(new
            {
                message = "Subscription payment is overdue. Premium modules are locked until your platform invoice is paid."
            })
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
            return;
        }

        var message = string.Equals(ModuleCode, TenantModuleCatalog.AiAssistantModuleCode, StringComparison.OrdinalIgnoreCase)
            ? "SheikhGo AI Assistant is not enabled for this company. Ask a platform admin to enable the AI module."
            : $"Module '{ModuleCode}' is not enabled for this company.";

        context.Result = new ObjectResult(new { message })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
