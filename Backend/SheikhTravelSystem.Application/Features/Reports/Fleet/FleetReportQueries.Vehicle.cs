using Dapper;
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

    private static async Task<ReportResponseDto> BuildVehicleReportAsync(
        System.Data.IDbConnection connection, int tenantId, int? branchId, int? departmentId,
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

        var clauses = new List<string> { "v.TenantId = @TenantId", "v.IsDeleted = 0" };
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        FleetReportSql.ApplyEffectiveVehicleScope(p, scope, null, branchId, departmentId, "v", clauses);
        var where = FleetReportSql.BuildWhere(clauses);

        var raw = await connection.QueryAsync<dynamic>(new CommandDefinition($"""
            SELECT v.Id, v.Name, v.RegistrationNumber AS Plate, v.VehicleCode AS FleetNo,
                v.Make, v.Model, v.Year, v.FuelType, v.CurrentMileage, v.InsuranceExpiryDate, v.Status,
                d.FullName AS DriverName, gd.Name AS TrackerName
            FROM Vehicles v
            OUTER APPLY (
                SELECT TOP 1 ah.DriverId
                FROM AssignmentHistory ah
                WHERE ah.VehicleId = v.Id AND ah.IsDeleted = 0 AND ah.Status = N'Active'
                ORDER BY ah.StartAt DESC
            ) activeAssign
            LEFT JOIN Drivers d ON d.Id = activeAssign.DriverId AND d.IsDeleted = 0
            LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
            {where}
            ORDER BY v.Name
            """, p, cancellationToken: ct));

        var rows = raw.Select(r =>
        {
            var vehicleStatus = (VehicleStatus)(int)r.Status;
            var statusLabel = VehicleStatusLabels.GetValueOrDefault(vehicleStatus, "Unknown");
            return (Row: FleetReportHelper.Row(
                ((int)r.Id).ToString(), (string)r.Name, 1, 0m,
                ("plate", (object?)r.Plate),
                ("fleetNo", (object?)(r.FleetNo ?? "—")),
                ("name", (object?)r.Name),
                ("make", (object?)(r.Make ?? "—")),
                ("model", (object?)(r.Model ?? "—")),
                ("year", (object?)r.Year),
                ("fuelType", (object?)FuelTypeLabel((int)r.FuelType)),
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
