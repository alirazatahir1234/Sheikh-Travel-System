using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.Maintenance;

public class WorkshopVendorTests
{
    [Fact]
    public void SerializeProducts_ReturnsJsonArray()
    {
        var json = VendorHelper.SerializeProducts(["Brake pads", "Oil filters"]);
        Assert.Equal("""["Brake pads","Oil filters"]""", json);
    }

    [Fact]
    public void ParseProducts_FromJsonArray()
    {
        var products = VendorHelper.ParseProducts("""["Tires","Batteries"]""");
        Assert.Equal(["Tires", "Batteries"], products);
    }

    [Fact]
    public void ParseProducts_FromCommaSeparated_Fallback()
    {
        var products = VendorHelper.ParseProducts("Fluids, Filters");
        Assert.Equal(["Fluids", "Filters"], products);
    }

    [Fact]
    public void VendorRow_ToDto_MapsProducts()
    {
        var row = new VendorRow(1, "Acme Parts", "Parts", "Ali", "0300", "a@x.com",
            """["Pads"]""", 4.5m, true, true);

        var dto = row.ToDto();
        Assert.Equal("Acme Parts", dto.Name);
        Assert.Single(dto.Products);
        Assert.True(dto.IsPreferred);
    }
}
