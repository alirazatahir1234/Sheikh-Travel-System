using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// SQL helpers for Stage 12 pilot enforcement (vehicles / drivers / reports).
/// </summary>
public static class DataScopeSql
{
    /// <summary>
    /// Intersects optional request filters with effective scope.
    /// Returns false when the caller asked for a branch/dept outside their scope.
    /// </summary>
    public static bool TryIntersectOptional(
        DataScopeResult scope,
        int? requestedBranchId,
        int? requestedDepartmentId,
        out int? effectiveBranchId,
        out int? effectiveDepartmentId,
        out string? error)
    {
        effectiveBranchId = requestedBranchId;
        effectiveDepartmentId = requestedDepartmentId;
        error = null;

        if (scope.IsCompanyWide)
            return true;

        if (requestedBranchId.HasValue)
        {
            if (scope.BranchIds.Count > 0 && !scope.BranchIds.Contains(requestedBranchId.Value))
            {
                error = "Branch is outside your data scope.";
                return false;
            }

            // Department-only scope without branch list: reject unknown branch unless company-wide.
            if (scope.Mode == DataScopeMode.Department && scope.BranchIds.Count == 0 && scope.DepartmentIds.Count > 0)
            {
                // Allow branch filter only when it won't widen past departments — leave to SQL IN on departments.
                // Keep requested branch if provided; department clamp still applies.
            }
        }

        if (requestedDepartmentId.HasValue)
        {
            if (scope.DepartmentIds.Count > 0 && !scope.DepartmentIds.Contains(requestedDepartmentId.Value))
            {
                error = "Department is outside your data scope.";
                return false;
            }
        }

        return true;
    }

    public static void ApplyVehicleScope(
        DynamicParameters p,
        DataScopeResult scope,
        string vehicleAlias,
        List<string> clauses,
        int? requestedBranchId = null,
        int? requestedDepartmentId = null)
    {
        if (!TryIntersectOptional(scope, requestedBranchId, requestedDepartmentId,
                out var branchId, out var departmentId, out _))
        {
            // Force empty result set for out-of-scope requests.
            clauses.Add("1 = 0");
            return;
        }

        if (scope.IsCompanyWide)
        {
            if (branchId.HasValue)
            {
                clauses.Add($"{vehicleAlias}.BranchId = @DsBranchId");
                p.Add("DsBranchId", branchId.Value);
            }

            if (departmentId.HasValue)
            {
                clauses.Add($"{vehicleAlias}.DepartmentId = @DsDepartmentId");
                p.Add("DsDepartmentId", departmentId.Value);
            }

            return;
        }

        if (scope.Mode == DataScopeMode.Department && scope.DepartmentIds.Count > 0)
        {
            if (departmentId.HasValue)
            {
                clauses.Add($"{vehicleAlias}.DepartmentId = @DsDepartmentId");
                p.Add("DsDepartmentId", departmentId.Value);
            }
            else
            {
                clauses.Add($"{vehicleAlias}.DepartmentId IN @DsDepartmentIds");
                p.Add("DsDepartmentIds", scope.DepartmentIds.ToArray());
            }

            if (branchId.HasValue)
            {
                clauses.Add($"{vehicleAlias}.BranchId = @DsBranchId");
                p.Add("DsBranchId", branchId.Value);
            }
            else if (scope.BranchIds.Count > 0)
            {
                clauses.Add($"({vehicleAlias}.BranchId IS NULL OR {vehicleAlias}.BranchId IN @DsBranchIds)");
                p.Add("DsBranchIds", scope.BranchIds.ToArray());
            }

            return;
        }

        if (scope.BranchIds.Count > 0)
        {
            if (branchId.HasValue)
            {
                // Explicit branch filter: still allow unassigned vehicles (NULL BranchId),
                // matching department-mode soft clamp — legacy fleet rows often have no branch.
                clauses.Add($"({vehicleAlias}.BranchId IS NULL OR {vehicleAlias}.BranchId = @DsBranchId)");
                p.Add("DsBranchId", branchId.Value);
            }
            else
            {
                clauses.Add($"({vehicleAlias}.BranchId IS NULL OR {vehicleAlias}.BranchId IN @DsBranchIds)");
                p.Add("DsBranchIds", scope.BranchIds.ToArray());
            }

            if (departmentId.HasValue)
            {
                clauses.Add($"{vehicleAlias}.DepartmentId = @DsDepartmentId");
                p.Add("DsDepartmentId", departmentId.Value);
            }
        }
    }

    public static void ApplyDriverScope(
        DynamicParameters p,
        DataScopeResult scope,
        string driverAlias,
        List<string> clauses,
        int? requestedBranchId = null)
    {
        if (!TryIntersectOptional(scope, requestedBranchId, null, out var branchId, out _, out _))
        {
            clauses.Add("1 = 0");
            return;
        }

        if (scope.IsCompanyWide)
        {
            if (branchId.HasValue)
            {
                clauses.Add($"{driverAlias}.BranchId = @DsBranchId");
                p.Add("DsBranchId", branchId.Value);
            }

            return;
        }

        // Drivers typically have BranchId only (no DepartmentId on many rows).
        if (scope.BranchIds.Count > 0)
        {
            if (branchId.HasValue)
            {
                clauses.Add($"{driverAlias}.BranchId = @DsBranchId");
                p.Add("DsBranchId", branchId.Value);
            }
            else
            {
                clauses.Add($"{driverAlias}.BranchId IN @DsBranchIds");
                p.Add("DsBranchIds", scope.BranchIds.ToArray());
            }
        }
        else if (scope.Mode == DataScopeMode.Department && scope.DepartmentIds.Count > 0)
        {
            // Soft: department-scoped users without branch list — no driver clamp if Drivers lack DepartmentId.
            // Prefer home/branch ids already collected; if empty, pass-through (avoid locking out).
        }
    }

    /// <summary>
    /// Soft fleet-linked scope for Bookings/Trips/Payments: clamp via joined vehicle/driver branch (or dept).
    /// Unassigned rows (no vehicle and no driver) remain visible so dispatchers can claim work.
    /// </summary>
    public static void ApplyLinkedFleetScope(
        DynamicParameters p,
        DataScopeResult scope,
        List<string> clauses,
        string vehicleAlias = "v",
        string driverAlias = "d")
    {
        if (scope.IsCompanyWide)
            return;

        if (scope.Mode == DataScopeMode.Department && scope.DepartmentIds.Count > 0)
        {
            clauses.Add($@"(
                ({vehicleAlias}.Id IS NULL AND {driverAlias}.Id IS NULL)
                OR {vehicleAlias}.DepartmentId IN @DsDepartmentIds
                OR ({vehicleAlias}.Id IS NULL AND {driverAlias}.BranchId IN @DsBranchIdsFallback)
            )");
            p.Add("DsDepartmentIds", scope.DepartmentIds.ToArray());
            p.Add("DsBranchIdsFallback", scope.BranchIds.Count > 0 ? scope.BranchIds.ToArray() : new[] { -1 });
            return;
        }

        if (scope.BranchIds.Count > 0)
        {
            clauses.Add($@"(
                ({vehicleAlias}.Id IS NULL AND {driverAlias}.Id IS NULL)
                OR {vehicleAlias}.BranchId IN @DsBranchIds
                OR {driverAlias}.BranchId IN @DsBranchIds
            )");
            p.Add("DsBranchIds", scope.BranchIds.ToArray());
        }
    }
}
