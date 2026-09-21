using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public partial class GetFleetReportQueryHandler
{
    private async Task<ReportResponseDto> BuildFuelReportAsync(
        int tenantId, DateTime from, DateTime to,
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

        var raw = await reportRepository.GetFuelReportRowsAsync(
            tenantId, from, to, vehicleId, branchId, scope, ct);

        var rows = raw.Select(r =>
        {
            decimal? odometerDelta = r.OdometerDelta;
            decimal liters = r.Liters;
            decimal? mileage = odometerDelta is > 0 ? Math.Round(liters / (decimal)odometerDelta * 100, 2) : null;

            return FleetReportHelper.Row(
                r.Id.ToString(), r.VehicleName, 1, r.TotalCost,
                ("date", (object?)r.FuelDate),
                ("vehicle", (object?)r.VehicleName),
                ("fuelType", (object?)FuelTypeLabel(r.FuelType)),
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
