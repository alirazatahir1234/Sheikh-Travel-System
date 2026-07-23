namespace SheikhTravelSystem.Application.Features.Platform;

/// <summary>
/// Stage 15 classification rules for API permission coverage inventory.
/// </summary>
public static class PermissionCoverageClassifier
{
    /// <summary>
    /// Controllers that intentionally use [Authorize] only (any authenticated tenant user).
    /// </summary>
    private static readonly HashSet<string> AuthOnlyAllowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "CompanyController",
        "DataScopeController",
        "ProfileController",
        "FeatureRegistryController",
        "TenantCompanyAliasController",
    };

    /// <summary>
    /// Controllers gated by role (not RequirePermission) — still considered Protected.
    /// </summary>
    private static readonly HashSet<string> RoleGatedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "DriverAppController",
        "CustomerPortalController",
        "CustomerPortalAuthController",
        "PortalPaymentsController",
    };

    private static readonly HashSet<string> InternalControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "DevController",
    };

    private static readonly HashSet<string> WriteHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE",
    };

    public static string Classify(
        string controllerName,
        string httpMethod,
        bool allowAnonymous,
        bool hasAuthorize,
        IReadOnlyList<string> permissionPolicies,
        IReadOnlyList<string> roles)
    {
        if (allowAnonymous)
            return PermissionCoverageStatuses.Public;

        if (InternalControllers.Contains(controllerName))
            return PermissionCoverageStatuses.Internal;

        if (RoleGatedControllers.Contains(controllerName) || roles.Count > 0)
            return PermissionCoverageStatuses.Protected;

        if (permissionPolicies.Count > 0)
        {
            if (WriteHttpMethods.Contains(httpMethod)
                && permissionPolicies.All(IsViewOnlyPermission)
                && !AuthOnlyAllowlist.Contains(controllerName))
            {
                return PermissionCoverageStatuses.PartiallyProtected;
            }

            return PermissionCoverageStatuses.Protected;
        }

        if (AuthOnlyAllowlist.Contains(controllerName))
            return PermissionCoverageStatuses.Protected;

        // Authenticated but no RequirePermission on a business controller.
        if (hasAuthorize)
            return PermissionCoverageStatuses.PartiallyProtected;

        return PermissionCoverageStatuses.Public;
    }

    public static string DeriveModule(string? permission, string controllerName)
    {
        if (!string.IsNullOrWhiteSpace(permission))
        {
            var head = permission.Split('.', 2)[0];
            return head switch
            {
                "Platform" => "Platform",
                "Vehicle" => "Fleet",
                "Driver" => "Fleet",
                "Maintenance" => "Fleet",
                "Gps" or "GPS" => "GPS",
                "Booking" or "Trip" or "Route" => "Travel",
                "Payment" or "Invoice" or "Fuel" => "Finance",
                "Customer" or "Report" => "CRM",
                "Ai" => "AI",
                "Notification" => "Notifications",
                _ => head
            };
        }

        var name = controllerName.Replace("Controller", "", StringComparison.OrdinalIgnoreCase);
        return name switch
        {
            "Vehicles" or "Fleet" or "Drivers" or "Assignments" => "Fleet",
            "Bookings" or "Trips" or "Routes" or "Pricing" => "Travel",
            "Payments" or "FuelLogs" => "Finance",
            "Customers" => "CRM",
            "GpsTracking" or "Tracking" or "Ops" => "GPS",
            "Ai" => "AI",
            "Auth" => "Auth",
            "Dev" => "Internal",
            "DriverApp" => "Mobile",
            "CustomerPortal" or "CustomerPortalAuth" or "PortalPayments" => "Portal",
            _ when name.StartsWith("Tenant", StringComparison.OrdinalIgnoreCase)
                 || name is "Company" or "License" or "Menus" or "Workspaces"
                     or "Dashboards" or "Security" or "DataScope" or "FeatureRegistry"
                     or "Branches" or "Departments" or "Roles" or "PermissionCatalog"
                     or "AccessTemplates" or "TenantAccess" or "TenantModules"
                     or "Subscription" => "Platform",
            _ => name
        };
    }

    public static bool IsBusinessController(string controllerName)
    {
        if (InternalControllers.Contains(controllerName)) return false;
        if (RoleGatedControllers.Contains(controllerName)) return false;
        if (AuthOnlyAllowlist.Contains(controllerName)) return false;
        if (controllerName.Equals("AuthController", StringComparison.OrdinalIgnoreCase)) return false;
        if (controllerName.Equals("LookupController", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static bool IsViewOnlyPermission(string permission)
    {
        var tail = permission.Split('.').LastOrDefault() ?? "";
        return tail.Equals("View", StringComparison.OrdinalIgnoreCase)
               || tail.EndsWith("View", StringComparison.OrdinalIgnoreCase);
    }
}
