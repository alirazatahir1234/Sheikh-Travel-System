using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

internal static class FleetReportSql
{
    public static void ApplyVehicleBranchFilters(
        DynamicParameters p, int? vehicleId, int? branchId, string vehicleAlias, string? branchAlias, List<string> clauses)
    {
        if (vehicleId.HasValue)
        {
            clauses.Add($"{vehicleAlias}.Id = @VehicleId");
            p.Add("VehicleId", vehicleId.Value);
        }

        if (branchId.HasValue)
        {
            var alias = branchAlias ?? vehicleAlias;
            clauses.Add($"{alias}.BranchId = @BranchId");
            p.Add("BranchId", branchId.Value);
        }
    }

    /// <summary>Stage 12: apply optional vehicle/branch filters intersected with effective data scope.</summary>
    public static void ApplyEffectiveVehicleScope(
        DynamicParameters p,
        DataScopeResult? scope,
        int? vehicleId,
        int? branchId,
        int? departmentId,
        string vehicleAlias,
        List<string> clauses)
    {
        if (vehicleId.HasValue)
        {
            clauses.Add($"{vehicleAlias}.Id = @VehicleId");
            p.Add("VehicleId", vehicleId.Value);
        }

        if (scope is null)
        {
            ApplyVehicleBranchFilters(p, null, branchId, vehicleAlias, null, clauses);
            ApplyDepartmentFilter(p, departmentId, vehicleAlias, clauses);
            return;
        }

        DataScopeSqlBuilder.ApplyVehicleScope(p, scope, vehicleAlias, clauses, branchId, departmentId);
    }

    public static void ApplyDepartmentFilter(
        DynamicParameters p, int? departmentId, string vehicleAlias, List<string> clauses)
    {
        if (departmentId.HasValue)
        {
            clauses.Add($"{vehicleAlias}.DepartmentId = @DepartmentId");
            p.Add("DepartmentId", departmentId.Value);
        }
    }

    public static string BuildWhere(List<string> clauses) =>
        clauses.Count > 0 ? $"WHERE {string.Join(" AND ", clauses)}" : string.Empty;
}
