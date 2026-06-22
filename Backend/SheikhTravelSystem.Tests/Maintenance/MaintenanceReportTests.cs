using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.Maintenance;

public class MaintenanceReportTests
{
    [Theory]
    [InlineData("summary", "cost-analysis")]
    [InlineData("vehicle", "vehicle-maintenance")]
    [InlineData("overdue", "overdue-maintenance")]
    [InlineData("cost-analysis", "cost-analysis")]
    public void NormalizeReportType_MapsAliases(string input, string expected) =>
        Assert.Equal(expected, MaintenanceReportHelper.NormalizeReportType(input));

    [Fact]
    public void ComputeNextRunAt_Weekly_AddsSevenDays()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var next = MaintenanceReportHelper.ComputeNextRunAt("Weekly", baseTime);
        Assert.Equal(baseTime.Date.AddHours(6).AddDays(7), next);
    }

    [Fact]
    public void ComputeNextRunAt_Monthly_AddsOneMonth()
    {
        var baseTime = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var next = MaintenanceReportHelper.ComputeNextRunAt("Monthly", baseTime);
        Assert.Equal(new DateTime(2026, 2, 15, 6, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Row_BuildsFieldsDictionary()
    {
        var row = MaintenanceReportHelper.Row("k1", "Label", 2, 100m,
            ("plate", "ABC-123"), ("totalCost", 100m));

        Assert.Equal("k1", row.Key);
        Assert.Equal(2, row.Count);
        Assert.Equal("ABC-123", row.Fields["plate"]);
        Assert.Equal(100m, row.Fields["totalCost"]);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("All", true)]
    [InlineData("Open", true)]
    [InlineData("Completed", false)]
    public void MatchesStatusFilter_Works(string? filter, bool expected) =>
        Assert.Equal(expected, MaintenanceReportHelper.MatchesStatusFilter(filter, "Open"));

    [Fact]
    public void ParseFilters_RoundTripsJson()
    {
        var filters = new MaintenanceReportFiltersDto(1, 2, "2026-01-01", "2026-01-31", "Open");
        var json = MaintenanceReportHelper.SerializeFilters(filters);
        var parsed = MaintenanceReportHelper.ParseFilters(json);
        Assert.Equal(1, parsed.VehicleId);
        Assert.Equal(2, parsed.BranchId);
        Assert.Equal("Open", parsed.Status);
    }

    [Fact]
    public void TitleFor_ReturnsHumanReadableTitle()
    {
        Assert.Equal("Cost Analysis Report", MaintenanceReportHelper.TitleFor("cost-analysis"));
        Assert.Equal("Breakdown Report", MaintenanceReportHelper.TitleFor("breakdown"));
    }
}
