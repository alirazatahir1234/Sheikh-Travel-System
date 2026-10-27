using SheikhTravelSystem.Application.Features.Trips.Commands;

namespace SheikhTravelSystem.Tests.Trips;

public class TripBulkImportParserTests
{
    [Fact]
    public void ParseBulkImportCsv_ParsesHeaderAndRow()
    {
        const string csv = """
            tripName,customerId,tripDate,plannedStart
            Airport Run,12,2026-11-01,2026-11-01T09:00:00
            """;

        var rows = BulkImportTripsCommandHandler.ParseBulkImportCsv(csv);
        Assert.Single(rows);
        Assert.Equal("Airport Run", rows[0].TripName);
        Assert.Equal(12, rows[0].CustomerId);
    }
}
