using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.MaintenanceModule;

public static class MaintenanceReportSql
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

    public static void ApplyEffectiveVehicleScope(
        DynamicParameters p,
        DataScopeResult? scope,
        int? vehicleId,
        int? branchId,
        string vehicleAlias,
        List<string> clauses)
    {
        if (scope is null)
        {
            ApplyVehicleBranchFilters(p, vehicleId, branchId, vehicleAlias, null, clauses);
            return;
        }

        if (vehicleId.HasValue)
        {
            clauses.Add($"{vehicleAlias}.Id = @VehicleId");
            p.Add("VehicleId", vehicleId.Value);
        }

        DataScopeSql.ApplyVehicleScope(p, scope, vehicleAlias, clauses, branchId, null);
    }

    public static string BuildWhere(List<string> clauses) =>
        clauses.Count > 0 ? $"WHERE {string.Join(" AND ", clauses)}" : string.Empty;
}
