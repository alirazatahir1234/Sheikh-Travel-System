using SheikhTravelSystem.Application.Features.Reports.Fleet;

namespace SheikhTravelSystem.Tests.Reports;

public class FleetReportHelperTests
{
    [Theory]
    [InlineData("trips", "trip")]
    [InlineData("vehicles", "vehicle")]
    [InlineData("drivers", "driver")]
    [InlineData("idling", "idle")]
    [InlineData("stops", "stop")]
    [InlineData("events", "event")]
    [InlineData("alerts", "alert")]
    [InlineData("maintenance", "maintenance")]
    public void NormalizeReportType_MapsAliases(string input, string expected) =>
        Assert.Equal(expected, FleetReportHelper.NormalizeReportType(input));

    [Fact]
    public void Row_BuildsFieldsDictionary()
    {
        var row = FleetReportHelper.Row("k1", "Label", 1, 42.5m,
            ("vehicle", "Toyota Corolla"), ("distance", 42.5m));

        Assert.Equal("k1", row.Key);
        Assert.Equal(1, row.Count);
        Assert.Equal(42.5m, row.TotalValue);
        Assert.Equal("Toyota Corolla", row.Fields["vehicle"]);
        Assert.Equal(42.5m, row.Fields["distance"]);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("All", true)]
    [InlineData("Open", true)]
    [InlineData("Closed", false)]
    public void MatchesStatusFilter_Works(string? filter, bool expected) =>
        Assert.Equal(expected, FleetReportHelper.MatchesStatusFilter(filter, "Open"));

    [Fact]
    public void TitleFor_ReturnsHumanReadableTitle()
    {
        Assert.Equal("Trip Report", FleetReportHelper.TitleFor("trip"));
        Assert.Equal("Event Report", FleetReportHelper.TitleFor("event"));
        Assert.Equal("Alert Report", FleetReportHelper.TitleFor("alert"));
        Assert.Equal("Vehicle Report", FleetReportHelper.TitleFor("vehicle"));
        Assert.Equal("Maintenance Report", FleetReportHelper.TitleFor("maintenance"));
        Assert.Equal("Fleet Report", FleetReportHelper.TitleFor("unknown-type"));
    }

    [Fact]
    public void ResolveDateRange_DefaultsToCurrentMonth()
    {
        var (from, to) = FleetReportHelper.ResolveDateRange(null, null);
        var now = DateTime.UtcNow;
        Assert.Equal(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(now.Date.AddDays(1), to);
    }
}
