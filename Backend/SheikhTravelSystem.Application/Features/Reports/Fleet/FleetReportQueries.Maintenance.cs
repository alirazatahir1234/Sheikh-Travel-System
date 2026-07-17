using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Application.Features.Reports.Fleet;

public partial class GetFleetReportQueryHandler
{
    /// <summary>
    /// Proxies the existing Maintenance Reports engine (GetMaintenanceReportQuery) — zero maintenance
    /// logic duplicated here, just a field-copy into the shared ReportResponseDto shape so Maintenance
    /// appears as one of the 10 Fleet Reports catalog entries. /fleet/maintenance/reports keeps
    /// working unchanged as its own separate entry point.
    /// </summary>
    private async Task<ReportResponseDto> BuildMaintenanceReportAsync(
        string? maintenanceReportType, DateTime from, DateTime to, int? vehicleId, int? branchId,
        string? status, CancellationToken ct)
    {
        var response = await mediator.Send(new GetMaintenanceReportQuery(
            maintenanceReportType ?? "cost-analysis", from, to, vehicleId, branchId, status), ct);

        var report = response.Data;
        if (report is null)
        {
            return new ReportResponseDto("maintenance", FleetReportHelper.TitleFor("maintenance"),
                [], [], 0, new Dictionary<string, object?>());
        }

        var columns = report.Columns.Select(c => new ReportColumnDto(c.Key, c.Label, c.Format)).ToList();
        var rows = report.Rows.Select(r => new ReportRowDto(r.Key, r.Label, r.Count, r.TotalCost, r.Fields)).ToList();

        return new ReportResponseDto(report.ReportType, report.Title, columns, rows, report.TotalCost, report.Summary);
    }
}
