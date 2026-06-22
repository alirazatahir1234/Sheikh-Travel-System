using SheikhTravelSystem.Application.Features.MaintenanceModule;

namespace SheikhTravelSystem.Tests.Maintenance;

public class MaintenanceServiceHistoryHelperTests
{
    [Fact]
    public void ExtractFirstDocumentUrl_FromStringArray_ReturnsFirstUrl()
    {
        var json = """["https://cdn.example.com/invoice1.pdf","https://cdn.example.com/invoice2.pdf"]""";
        Assert.Equal("https://cdn.example.com/invoice1.pdf", MaintenanceServiceHistoryHelper.ExtractFirstDocumentUrl(json));
    }

    [Fact]
    public void ExtractFirstDocumentUrl_FromObjectArray_ReturnsUrlProperty()
    {
        var json = """[{"name":"invoice.pdf","url":"https://cdn.example.com/inv.pdf"}]""";
        Assert.Equal("https://cdn.example.com/inv.pdf", MaintenanceServiceHistoryHelper.ExtractFirstDocumentUrl(json));
    }

    [Fact]
    public void ExtractFirstDocumentUrl_FromPlainUrl_ReturnsUrl()
    {
        Assert.Equal("https://cdn.example.com/doc.pdf", MaintenanceServiceHistoryHelper.ExtractFirstDocumentUrl("https://cdn.example.com/doc.pdf"));
    }

    [Fact]
    public void ExtractFirstDocumentUrl_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(MaintenanceServiceHistoryHelper.ExtractFirstDocumentUrl(null));
        Assert.Null(MaintenanceServiceHistoryHelper.ExtractFirstDocumentUrl(""));
    }

    [Fact]
    public void MapRow_PopulatesInvoiceUrlFromDocumentsJson()
    {
        var row = new ServiceHistoryRow(
            1, "WorkOrder", 10, "Bus 1", "ABC-123", "Oil Change",
            new DateTime(2025, 6, 1), "Main Workshop", "Ali Khan",
            500m, 300m, 200m,
            """["https://cdn.example.com/invoice.pdf"]""",
            "Completed service", "Completed");

        var dto = MaintenanceServiceHistoryHelper.MapRow(row);

        Assert.Equal("WorkOrder", dto.Source);
        Assert.Equal("Oil Change", dto.ServiceType);
        Assert.Equal("https://cdn.example.com/invoice.pdf", dto.InvoiceUrl);
        Assert.Equal("Ali Khan", dto.TechnicianName);
        Assert.Equal(500m, dto.TotalCost);
    }
}
