using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Platform;
using System.Reflection;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(PlatformPermissions.SecurityManage)]
[ApiController]
[Route("api/platform/permission-coverage")]
public class PermissionCoverageController(IActionDescriptorCollectionProvider actionDescriptors) : BaseApiController
{
    [HttpGet]
    public IActionResult GetCoverage()
    {
        var endpoints = new List<PermissionCoverageEndpointDto>();

        foreach (var descriptor in actionDescriptors.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
        {
            var controllerName = descriptor.ControllerName + "Controller";
            var httpMethod = descriptor.EndpointMetadata
                .OfType<HttpMethodMetadata>()
                .SelectMany(m => m.HttpMethods)
                .FirstOrDefault()
                ?? InferHttpMethod(descriptor)
                ?? "GET";

            var route = BuildRoute(descriptor);
            var allowAnonymous = HasAttribute<AllowAnonymousAttribute>(descriptor);
            var authorizeAttrs = GetAttributes<AuthorizeAttribute>(descriptor)
                .Where(a => a is not RequirePermissionAttribute)
                .ToList();
            var requirePerms = GetAttributes<RequirePermissionAttribute>(descriptor).ToList();
            var permissionPolicies = requirePerms
                .Select(a => a.Policy)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Also pick up AuthorizeAttribute.Policy when set without RequirePermissionAttribute wrapper.
            foreach (var auth in GetAttributes<AuthorizeAttribute>(descriptor))
            {
                if (!string.IsNullOrWhiteSpace(auth.Policy)
                    && !permissionPolicies.Contains(auth.Policy, StringComparer.OrdinalIgnoreCase)
                    && LooksLikePermission(auth.Policy))
                {
                    permissionPolicies.Add(auth.Policy);
                }
            }

            var roles = GetAttributes<AuthorizeAttribute>(descriptor)
                .SelectMany(a => (a.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hasAuthorize = authorizeAttrs.Count > 0
                               || permissionPolicies.Count > 0
                               || roles.Count > 0
                               || HasAttribute<AuthorizeAttribute>(descriptor);

            var status = PermissionCoverageClassifier.Classify(
                controllerName,
                httpMethod,
                allowAnonymous,
                hasAuthorize,
                permissionPolicies,
                roles);

            var primaryPermission = permissionPolicies.FirstOrDefault()
                                    ?? (roles.Count > 0 ? $"Roles:{string.Join('|', roles)}" : null);

            string? notes = null;
            if (status == PermissionCoverageStatuses.Protected && permissionPolicies.Count == 0 && roles.Count == 0)
                notes = "Protected-by-auth";
            else if (status == PermissionCoverageStatuses.Protected && roles.Count > 0)
                notes = "Protected-via-role";

            endpoints.Add(new PermissionCoverageEndpointDto(
                PermissionCoverageClassifier.DeriveModule(primaryPermission, controllerName),
                controllerName,
                descriptor.ActionName,
                httpMethod.ToUpperInvariant(),
                route,
                primaryPermission,
                status,
                notes));
        }

        endpoints = endpoints
            .OrderBy(e => e.Module, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Controller, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Route, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.HttpMethod, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new PermissionCoverageReportDto(
            endpoints.Count,
            endpoints.Count(e => e.CoverageStatus == PermissionCoverageStatuses.Protected),
            endpoints.Count(e => e.CoverageStatus == PermissionCoverageStatuses.PartiallyProtected),
            endpoints.Count(e => e.CoverageStatus == PermissionCoverageStatuses.Public),
            endpoints.Count(e => e.CoverageStatus == PermissionCoverageStatuses.Internal),
            endpoints);

        return Ok(ApiResponse<PermissionCoverageReportDto>.SuccessResponse(report));
    }

    private static bool LooksLikePermission(string policy)
        => policy.Contains('.', StringComparison.Ordinal);

    private static string BuildRoute(ControllerActionDescriptor descriptor)
    {
        var template = descriptor.AttributeRouteInfo?.Template;
        if (!string.IsNullOrWhiteSpace(template))
            return "/" + template.TrimStart('/');

        return $"/api/{descriptor.ControllerName}";
    }

    private static string? InferHttpMethod(ControllerActionDescriptor descriptor)
    {
        foreach (var meta in descriptor.EndpointMetadata)
        {
            if (meta is HttpMethodMetadata hm)
                return hm.HttpMethods.FirstOrDefault();
        }

        var name = descriptor.ActionName;
        if (name.StartsWith("Get", StringComparison.OrdinalIgnoreCase)) return "GET";
        if (name.StartsWith("Post", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Create", StringComparison.OrdinalIgnoreCase)) return "POST";
        if (name.StartsWith("Put", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Update", StringComparison.OrdinalIgnoreCase)) return "PUT";
        if (name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)) return "DELETE";
        return null;
    }

    private static bool HasAttribute<T>(ControllerActionDescriptor descriptor) where T : Attribute
        => GetAttributes<T>(descriptor).Any();

    private static IEnumerable<T> GetAttributes<T>(ControllerActionDescriptor descriptor) where T : Attribute
    {
        foreach (var a in descriptor.MethodInfo.GetCustomAttributes(typeof(T), inherit: true).OfType<T>())
            yield return a;
        foreach (var a in descriptor.ControllerTypeInfo.GetCustomAttributes(typeof(T), inherit: true).OfType<T>())
            yield return a;
        foreach (var a in descriptor.EndpointMetadata.OfType<T>())
            yield return a;
    }
}
