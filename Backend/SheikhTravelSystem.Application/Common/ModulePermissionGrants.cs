namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Permissions a company's administrator roles receive the moment an opt-in module is enabled for it.
/// Core modules are covered by <see cref="TenantRolePermissionTemplates"/> at provisioning time; the opt-in
/// modules listed here are deliberately kept out of the templates (the permission engine would otherwise warn
/// about non-entitled grants), so without this map a company enabled later would have no way into the module
/// until an admin edits roles by hand. Grants are additive and idempotent; disabling a module never revokes.
/// </summary>
public static class ModulePermissionGrants
{
    /// <summary>System roles that receive the module's permissions (per tenant, by role code).</summary>
    public static readonly string[] Roles = ["TENANT_ADMIN", "OWNER"];

    private static readonly Dictionary<string, string[]> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [TenantModuleCatalog.TravelAgencyModuleCode] = TravelPermissions.All,
        [TenantModuleCatalog.RentalModuleCode] = RentalPermissions.All,
    };

    /// <summary>Module codes that carry a grant.</summary>
    public static IReadOnlyCollection<string> Modules => Map.Keys;

    public static IReadOnlyList<string> PermissionsFor(string moduleCode)
        => Map.TryGetValue(moduleCode, out var codes) ? codes : [];

    /// <summary>Distinct permission codes for every module in <paramref name="moduleCodes"/> that carries a grant.</summary>
    public static IReadOnlyList<string> CodesFor(IEnumerable<string> moduleCodes)
        => moduleCodes.SelectMany(PermissionsFor).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Modules in <paramref name="after"/> that were not in <paramref name="before"/> and carry a grant.</summary>
    public static IReadOnlyList<string> NewlyEnabled(IEnumerable<string> before, IEnumerable<string> after)
    {
        var had = before.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return after.Where(c => !had.Contains(c) && Map.ContainsKey(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
