using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public partial class GetFleetReportQueryHandler
{
    private static readonly Dictionary<VehicleStatus, string> VehicleStatusLabels = new()
    {
        [VehicleStatus.Available] = "Available",
        [VehicleStatus.OnTrip] = "On Trip",
        [VehicleStatus.Maintenance] = "Maintenance",
        [VehicleStatus.Retired] = "Retired",
        [VehicleStatus.Draft] = "Draft"
    };

    private async Task<ReportResponseDto> BuildVehicleReportAsync(
        int tenantId, int? branchId, int? departmentId,
        string? status, CancellationToken ct, DataScopeResult? scope = null)
    {
        // Registration Expiry is intentionally not a column — the schema only tracks Insurance
        // Expiry (v.InsuranceExpiryDate); a Registration Expiry date doesn't exist anywhere in this
        // system and is never fabricated here.
        var columns = new[]
        {
            new ReportColumnDto("plate", "Plate", "text"),
            new ReportColumnDto("fleetNo", "Fleet #", "text"),
            new ReportColumnDto("name", "Name", "text"),
            new ReportColumnDto("make", "Make", "text"),
            new ReportColumnDto("model", "Model", "text"),
            new ReportColumnDto("year", "Year", "number"),
            new ReportColumnDto("fuelType", "Fuel Type", "text"),
            new ReportColumnDto("driver", "Driver", "text"),
            new ReportColumnDto("tracker", "Tracker", "text"),
            new ReportColumnDto("status", "Status", "text"),
            new ReportColumnDto("mileage", "Mileage", "number"),
            new ReportColumnDto("insuranceExpiry", "Insurance Expiry", "date")
        };

        var raw = await reportRepository.GetVehicleReportRowsAsync(
            tenantId, branchId, departmentId, scope, ct);

        var rows = raw.Select(r =>
        {
            var vehicleStatus = (VehicleStatus)r.Status;
            var statusLabel = VehicleStatusLabels.GetValueOrDefault(vehicleStatus, "Unknown");
            return (Row: FleetReportHelper.Row(
                r.Id.ToString(), r.Name, 1, 0m,
                ("plate", (object?)r.Plate),
                ("fleetNo", (object?)(r.FleetNo ?? "—")),
                ("name", (object?)r.Name),
                ("make", (object?)(r.Make ?? "—")),
                ("model", (object?)(r.Model ?? "—")),
                ("year", (object?)r.Year),
                ("fuelType", (object?)FuelTypeLabel(r.FuelType)),
                ("driver", (object?)(r.DriverName ?? "—")),
                ("tracker", (object?)(r.TrackerName ?? "—")),
                ("status", (object?)statusLabel),
                ("mileage", (object?)r.CurrentMileage),
                ("insuranceExpiry", (object?)r.InsuranceExpiryDate)),
                Status: statusLabel);
        }).Where(x => FleetReportHelper.MatchesStatusFilter(status, x.Status))
          .Select(x => x.Row).ToList();

        var summary = new Dictionary<string, object?>
        {
            ["totalVehicles"] = rows.Count,
            ["available"] = rows.Count(r => (string?)r.Fields["status"] == "Available"),
            ["onTrip"] = rows.Count(r => (string?)r.Fields["status"] == "On Trip"),
            ["maintenance"] = rows.Count(r => (string?)r.Fields["status"] == "Maintenance"),
            ["retired"] = rows.Count(r => (string?)r.Fields["status"] == "Retired"),
            ["unassigned"] = rows.Count(r => (string?)r.Fields["driver"] == "—")
        };

        return new ReportResponseDto("vehicle", FleetReportHelper.TitleFor("vehicle"), columns, rows, 0, summary);
    }

    private static string FuelTypeLabel(int fuelType) => (FuelType)fuelType switch
    {
        FuelType.Petrol => "Petrol",
        FuelType.Diesel => "Diesel",
        FuelType.CNG => "CNG",
        _ => "Unknown"
    };
}
