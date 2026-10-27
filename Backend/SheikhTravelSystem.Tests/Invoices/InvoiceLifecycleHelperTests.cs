using FluentAssertions;
using SheikhTravelSystem.Application.Features.Invoices;

namespace SheikhTravelSystem.Tests.Invoices;

public class InvoiceLifecycleHelperTests
{
    [Fact]
    public void ResolvePaymentStatus_marks_overdue_after_grace()
    {
        var issued = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var due = issued.AddDays(14);
        var asOf = due.AddDays(3);

        InvoiceLifecycleHelper.ResolvePaymentStatus("Pending", 100m, due, issued, asOf)
            .Should().Be("Overdue");
    }

    [Fact]
    public void ResolvePaymentStatus_pending_before_grace()
    {
        var issued = DateTime.UtcNow.AddDays(-2);
        var due = issued.AddDays(14);

        InvoiceLifecycleHelper.ResolvePaymentStatus("Pending", 50m, due, issued, DateTime.UtcNow)
            .Should().Be("Pending");
    }

    [Fact]
    public void NormalizeStoredStatus_maps_legacy_issued()
    {
        InvoiceLifecycleHelper.NormalizeStoredStatus("Issued").Should().Be("Pending");
    }
}
