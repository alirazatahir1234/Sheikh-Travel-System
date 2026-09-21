using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Pure data-scope intersection helpers (no SQL / Dapper).
/// SQL clause builders live in Infrastructure <c>Persistence.DataScopeSql</c>.
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
}
