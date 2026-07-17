using System.Data;
using Dapper;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public static class FleetReportSql
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

    public static void ApplyDepartmentFilter(
        DynamicParameters p, int? departmentId, string vehicleAlias, List<string> clauses)
    {
        if (departmentId.HasValue)
        {
            clauses.Add($"{vehicleAlias}.DepartmentId = @DepartmentId");
            p.Add("DepartmentId", departmentId.Value);
        }
    }

    /// <summary>
    /// Vehicles has no DriverId column — current/period driver assignment lives in AssignmentHistory
    /// (time-window join), same pattern GetDriverScoreRankingQueryHandler uses. Resolves the set of
    /// VehicleIds a driver was assigned to at any point overlapping [from, to), for post-filtering
    /// report rows that only carry VehicleId natively.
    /// </summary>
    public static async Task<HashSet<int>> ResolveVehicleIdsForDriverAsync(
        IDbConnection connection, int tenantId, int driverId, DateTime from, DateTime to, CancellationToken ct)
    {
        var ids = await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT DISTINCT VehicleId FROM AssignmentHistory
            WHERE TenantId = @TenantId AND IsDeleted = 0 AND DriverId = @DriverId
              AND StartAt <= @To AND (EndAt IS NULL OR EndAt >= @From)
            """, new { TenantId = tenantId, DriverId = driverId, From = from, To = to }, cancellationToken: ct));
        return ids.ToHashSet();
    }

    public static string BuildWhere(List<string> clauses) =>
        clauses.Count > 0 ? $"WHERE {string.Join(" AND ", clauses)}" : string.Empty;
}
