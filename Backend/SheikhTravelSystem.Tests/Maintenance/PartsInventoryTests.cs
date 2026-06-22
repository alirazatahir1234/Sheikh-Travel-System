using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.Maintenance;

public class PartsInventoryTests
{
    [Theory]
    [InlineData(0, 5, PartStockHelper.StatusOutOfStock, false, true)]
    [InlineData(3, 5, PartStockHelper.StatusLowStock, true, false)]
    [InlineData(5, 5, PartStockHelper.StatusInStock, false, false)]
    [InlineData(10, 5, PartStockHelper.StatusInStock, false, false)]
    public void ComputeStockStatus_MapsCorrectly(
        int stock, int min, string expectedStatus, bool expectedLow, bool expectedOut)
    {
        Assert.Equal(expectedStatus, PartStockHelper.ComputeStockStatus(stock, min));
        Assert.Equal(expectedLow, PartStockHelper.IsLowStock(stock, min));
        Assert.Equal(expectedOut, PartStockHelper.IsOutOfStock(stock));
    }

    [Fact]
    public void SerializeAndParseCompatibility_RoundTrips()
    {
        var json = PartStockHelper.SerializeCompatibility(["Toyota Hiace", "Ford Transit"]);
        var items = PartStockHelper.ParseCompatibility(json);
        Assert.Equal(2, items.Count);
        Assert.Contains("Toyota Hiace", items);
        Assert.Contains("Ford Transit", items);
    }

    [Fact]
    public void ParseCompatibility_FallsBackToCommaSeparated()
    {
        var items = PartStockHelper.ParseCompatibility("Fluids, Filters");
        Assert.Equal(["Fluids", "Filters"], items);
    }

    [Fact]
    public void PartRow_ToDto_MapsStockStatus()
    {
        var row = new PartRow(1, "P-001", "Oil Filter", "Fluids", null, null, 12.5m, 5, 2,
            """["Hiace"]""", "Warehouse A");

        var dto = row.ToDto();

        Assert.Equal(PartStockHelper.StatusLowStock, dto.StockStatus);
        Assert.True(dto.IsLowStock);
        Assert.False(dto.IsOutOfStock);
        Assert.Single(dto.VehicleCompatibility);
        Assert.Equal("Warehouse A", dto.Location);
    }

    [Fact]
    public void IssuePart_InsufficientStock_MessageIsClear()
    {
        const string message = "Insufficient stock for this issue.";
        Assert.Contains("Insufficient stock", message);
    }
}
