using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Pure resolution for Stage 12 Data Scope (unit-testable without DB).
/// </summary>
public static class DataScopeResolver
{
    public const string ScopeCompany = "Company";
    public const string ScopeBranch = "Branch";
    public const string ScopeDepartment = "Department";
    public const string ScopeAssigned = "Assigned";

    public sealed record RoleAssignmentInput(
        string RoleCode,
        string? ScopeLevel,
        int? BranchId,
        int? DepartmentId);

    public static DataScopeResult Resolve(
        int userId,
        int tenantId,
        int? homeBranchId,
        int? homeDepartmentId,
        IReadOnlyList<RoleAssignmentInput> assignments)
    {
        var codes = assignments
            .Select(a => a.RoleCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codes.Any(c => string.Equals(c, PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            return CompanyWide(userId, tenantId, homeBranchId, homeDepartmentId, "super_admin");
        }

        if (codes.Any(c => string.Equals(c, PlatformRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            return CompanyWide(userId, tenantId, homeBranchId, homeDepartmentId, "company_admin");
        }

        // GPS operators (and any Company-scoped role) need fleet-wide visibility even when the
        // same user also has FLEET_MANAGER/DISPATCHER with Branch scope — otherwise Live Map
        // (company roster) and Vehicles (branch clamp) disagree, and "View details" looks broken.
        if (codes.Any(c => string.Equals(c, "GPS_OPERATOR", StringComparison.OrdinalIgnoreCase)))
        {
            return CompanyWide(userId, tenantId, homeBranchId, homeDepartmentId, "gps_operator");
        }

        // Operational fleet roles: company-wide list (matches Fleet dashboard totals). Branch
        // managers keep Branch scope via BRANCH_MANAGER only.
        if (codes.Any(c =>
                string.Equals(c, "FLEET_MANAGER", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, "DISPATCHER", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c, "DRIVER_MANAGER", StringComparison.OrdinalIgnoreCase)))
        {
            return CompanyWide(userId, tenantId, homeBranchId, homeDepartmentId, "fleet_ops");
        }

        // Any Company-level role → company-wide (ignore accidental UserRoles.BranchId clamps).
        foreach (var a in assignments)
        {
            if (!IsCompanyLevel(a.ScopeLevel)) continue;
            return CompanyWide(userId, tenantId, homeBranchId, homeDepartmentId, "roles");
        }

        var branchIds = new HashSet<int>();
        var departmentIds = new HashSet<int>();

        foreach (var a in assignments)
        {
            var branchId = a.BranchId ?? homeBranchId;
            var departmentId = a.DepartmentId ?? homeDepartmentId;

            // ScopeLevel Branch prefers branch; Department prefers department.
            var level = NormalizeScopeLevel(a.ScopeLevel);
            if (string.Equals(level, ScopeDepartment, StringComparison.OrdinalIgnoreCase))
            {
                if (departmentId.HasValue) departmentIds.Add(departmentId.Value);
                else if (branchId.HasValue) branchIds.Add(branchId.Value);
            }
            else if (string.Equals(level, ScopeBranch, StringComparison.OrdinalIgnoreCase))
            {
                if (branchId.HasValue) branchIds.Add(branchId.Value);
            }
            else
            {
                // Assigned (default): collect whatever is present.
                if (departmentId.HasValue) departmentIds.Add(departmentId.Value);
                if (branchId.HasValue) branchIds.Add(branchId.Value);
            }
        }

        // No role rows: fall back to home org only.
        if (assignments.Count == 0)
        {
            if (homeDepartmentId.HasValue) departmentIds.Add(homeDepartmentId.Value);
            if (homeBranchId.HasValue) branchIds.Add(homeBranchId.Value);
        }

        if (departmentIds.Count > 0)
        {
            return new DataScopeResult(
                userId,
                tenantId,
                DataScopeMode.Department,
                IsCompanyWide: false,
                BranchIds: branchIds.OrderBy(x => x).ToList(),
                DepartmentIds: departmentIds.OrderBy(x => x).ToList(),
                HomeBranchId: homeBranchId,
                HomeDepartmentId: homeDepartmentId,
                Source: "roles");
        }

        if (branchIds.Count > 0)
        {
            return new DataScopeResult(
                userId,
                tenantId,
                DataScopeMode.Branch,
                IsCompanyWide: false,
                BranchIds: branchIds.OrderBy(x => x).ToList(),
                DepartmentIds: Array.Empty<int>(),
                HomeBranchId: homeBranchId,
                HomeDepartmentId: homeDepartmentId,
                Source: assignments.Count > 0 ? "roles" : "user");
        }

        // Soft pass-through for unscoped legacy users.
        return CompanyWide(userId, tenantId, homeBranchId, homeDepartmentId, "default");
    }

    public static bool IsCompanyLevel(string? scopeLevel)
    {
        var normalized = NormalizeScopeLevel(scopeLevel);
        return string.Equals(normalized, ScopeCompany, StringComparison.OrdinalIgnoreCase)
            // Legacy alias used by early GPS_OPERATOR seed rows.
            || string.Equals(normalized, "Tenant", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeScopeLevel(string? scopeLevel)
    {
        if (string.IsNullOrWhiteSpace(scopeLevel)) return ScopeAssigned;
        return scopeLevel.Trim();
    }

    public static string DefaultScopeLevelForRoleCode(string roleCode)
    {
        if (string.Equals(roleCode, PlatformRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleCode, PlatformRoles.TenantAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleCode, "GPS_OPERATOR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleCode, "FLEET_MANAGER", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleCode, "DISPATCHER", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleCode, "DRIVER_MANAGER", StringComparison.OrdinalIgnoreCase))
            return ScopeCompany;

        if (string.Equals(roleCode, "BRANCH_MANAGER", StringComparison.OrdinalIgnoreCase))
            return ScopeBranch;

        return ScopeAssigned;
    }

    private static DataScopeResult CompanyWide(
        int userId, int tenantId, int? homeBranchId, int? homeDepartmentId, string source)
        => new(
            userId,
            tenantId,
            DataScopeMode.Company,
            IsCompanyWide: true,
            BranchIds: Array.Empty<int>(),
            DepartmentIds: Array.Empty<int>(),
            HomeBranchId: homeBranchId,
            HomeDepartmentId: homeDepartmentId,
            Source: source);
}
