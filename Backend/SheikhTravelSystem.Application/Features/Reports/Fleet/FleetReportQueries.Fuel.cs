using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public partial class GetFleetReportQueryHandler
{
    private static async Task<ReportResponseDto> BuildFuelReportAsync(
        System.Data.IDbConnection connection, int tenantId, DateTime from, DateTime to,
        int? vehicleId, int? branchId, CancellationToken ct, DataScopeResult? scope = null)
    {
        var columns = new[]
        {
            new ReportColumnDto("date", "Date", "date"),
            new ReportColumnDto("vehicle", "Vehicle", "text"),
            new ReportColumnDto("fuelType", "Fuel Type", "text"),
            new ReportColumnDto("liters", "Liters", "number"),
            new ReportColumnDto("price", "Price/Liter", "currency"),
            new ReportColumnDto("cost", "Cost", "currency"),
            new ReportColumnDto("odometer", "Odometer", "number"),
            new ReportColumnDto("mileage", "Mileage (L/100km)", "number")
        };

        var clauses = new List<string> { "f.TenantId = @TenantId", "f.IsDeleted = 0", "f.FuelDate >= @From", "f.FuelDate < @To" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("From", from);
        p.Add("To", to);
        FleetReportSql.ApplyEffectiveVehicleScope(p, scope, vehicleId, branchId, null, "v", clauses);
        var where = FleetReportSql.BuildWhere(clauses);

        // Per-row consumption from the odometer delta since this vehicle's previous fill-up
        // (LAG over FuelDate) — SQL Server window function, same dialect used throughout this
        // codebase (GETUTCDATE/DATEDIFF elsewhere confirm SQL Server, so LAG is safely supported).
        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT f.Id, f.FuelDate, v.Name AS VehicleName, f.FuelType, f.Liters, f.PricePerLiter, f.TotalCost,
                f.OdometerReading,
                f.OdometerReading - LAG(f.OdometerReading) OVER (PARTITION BY f.VehicleId ORDER BY f.FuelDate) AS OdometerDelta
            FROM FuelLogs f
            INNER JOIN Vehicles v ON v.Id = f.VehicleId
            {where}
            ORDER BY v.Name, f.FuelDate
            """, p, cancellationToken: ct));

        var rows = raw.Select(r =>
        {
            decimal? odometerDelta = r.OdometerDelta;
            decimal liters = r.Liters;
            decimal? mileage = odometerDelta is > 0 ? Math.Round(liters / (decimal)odometerDelta * 100, 2) : null;

            return FleetReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.VehicleName, 1, (decimal)r.TotalCost,
                ("date", (object?)r.FuelDate),
                ("vehicle", (object?)r.VehicleName),
                ("fuelType", (object?)FuelTypeLabel((int)r.FuelType)),
                ("liters", (object?)liters),
                ("price", (object?)r.PricePerLiter),
                ("cost", (object?)r.TotalCost),
                ("odometer", (object?)r.OdometerReading),
                ("mileage", (object?)(mileage.HasValue ? (object)mileage.Value : "—")));
        }).ToList();

        var totalLiters = rows.Sum(r => (decimal)r.Fields["liters"]!);
        var totalCost = rows.Sum(r => r.TotalValue);
        var withMileage = rows.Where(r => r.Fields["mileage"] is decimal).Select(r => (decimal)r.Fields["mileage"]!).ToList();

        var summary = new Dictionary<string, object?>
        {
            ["totalLiters"] = totalLiters,
            ["totalCost"] = totalCost,
            ["averageConsumption"] = withMileage.Count > 0 ? Math.Round(withMileage.Average(), 2) : (object?)null
        };

        return new ReportResponseDto("fuel", FleetReportHelper.TitleFor("fuel"), columns, rows, totalCost, summary);
    }
}
