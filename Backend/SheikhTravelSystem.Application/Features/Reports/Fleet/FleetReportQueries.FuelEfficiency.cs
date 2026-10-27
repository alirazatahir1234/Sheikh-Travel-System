using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;
using SheikhTravelSystem.Application.Features.FuelLogs.Queries;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public partial class GetFleetReportQueryHandler
{
    private async Task<ReportResponseDto> BuildFuelEfficiencyReportAsync(
        DateTime from, DateTime toExclusive, CancellationToken ct)
    {
        var response = await mediator.Send(new GetFuelEfficiencyReportForRangeQuery(from, toExclusive), ct);
        if (!response.Success || response.Data is null)
        {
            return new ReportResponseDto(
                "fuel-efficiency",
                FleetReportHelper.TitleFor("fuel-efficiency"),
                [],
                [],
                0,
                new Dictionary<string, object?> { ["error"] = response.Message });
        }

        var data = response.Data;
        var columns = new[]
        {
            new ReportColumnDto("group", "Group", "text"),
            new ReportColumnDto("label", "Name", "text"),
            new ReportColumnDto("distanceKm", "Distance (km)", "number"),
            new ReportColumnDto("liters", "Liters", "number"),
            new ReportColumnDto("kmPerLiter", "Km/L", "number"),
            new ReportColumnDto("basis", "Basis", "text")
        };

        static IEnumerable<ReportRowDto> MapRows(string group, IReadOnlyList<FuelEfficiencyRowDto> rows) =>
            rows.Select(r => FleetReportHelper.Row(
                $"{group}:{r.GroupKey}",
                r.Label,
                1,
                r.Liters,
                ("group", group),
                ("label", r.Label),
                ("distanceKm", (object?)(r.DistanceKm ?? (object)"—")),
                ("liters", r.Liters),
                ("kmPerLiter", (object?)(r.KmPerLiter ?? (object)"—")),
                ("basis", r.BasisNote)));

        var allRows = MapRows("Vehicle", data.ByVehicle)
            .Concat(MapRows("Driver", data.ByDriver))
            .Concat(MapRows("Route", data.ByRoute))
            .ToList();

        var summary = new Dictionary<string, object?>
        {
            ["vehicleRows"] = data.ByVehicle.Count,
            ["driverRows"] = data.ByDriver.Count,
            ["routeRows"] = data.ByRoute.Count,
            ["periodFrom"] = data.From,
            ["periodTo"] = data.To
        };

        return new ReportResponseDto(
            "fuel-efficiency",
            FleetReportHelper.TitleFor("fuel-efficiency"),
            columns,
            allRows,
            allRows.Sum(r => r.TotalValue),
            summary);
    }
}
