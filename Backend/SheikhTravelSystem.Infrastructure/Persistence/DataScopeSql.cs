using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Dapper SQL helpers for Stage 12 pilot enforcement (vehicles / drivers / reports).
/// </summary>
public static class DataScopeSqlBuilder
{
    public static void ApplyVehicleScope(
        DynamicParameters p,
        DataScopeResult scope,
        string vehicleAlias,
        List<string> clauses,
        int? requestedBranchId = null,
        int? requestedDepartmentId = null)
    {
        if (!Application.Common.DataScopeSql.TryIntersectOptional(scope, requestedBranchId, requestedDepartmentId,
                out var branchId, out var departmentId, out _))
        {
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
        if (!Application.Common.DataScopeSql.TryIntersectOptional(scope, requestedBranchId, null, out var branchId, out _, out _))
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
    }

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
