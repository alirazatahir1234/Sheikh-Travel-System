using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices.Commands;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;
using SheikhTravelSystem.Application.Features.Invoices.Services;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Invoices;

public class InvoiceCommandsTests
{
    [Fact]
    public async Task IssueFromBooking_service_creates_pending_invoice_with_balance()
    {
        var repo = new Mock<ICustomerInvoiceRepository>();
        tenant_setup(out var tenant);
        repo.Setup(r => r.GetBookingSnapshotAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceBookingSnapshot
            {
                BookingId = 42,
                CustomerId = 7,
                TotalAmount = 1000,
                BookingNumber = "BK-42",
                CustomerName = "Ali",
                Status = (int)BookingStatus.Completed
            });
        repo.Setup(r => r.HasActiveInvoiceForBookingAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.GetTotalPaidForBookingAsync(42, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(250m);
        repo.Setup(r => r.AllocateInvoiceNumberAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("INV-2026-0001");
        repo.Setup(r => r.CreateAsync(
                1, 9, It.IsAny<InvoiceCreatePayload>(), "INV-2026-0001",
                250m, 750m, InvoiceStatus.Pending, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);

        var service = new InvoiceIssueService(repo.Object);
        var result = await service.IssueFromBookingAsync(1, 9, 42, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(11);
    }

    [Fact]
    public async Task Void_rejects_paid_invoice()
    {
        var repo = new Mock<ICustomerInvoiceRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetByIdAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceDetailDto(
                5, 1, null, 1, "INV-1", 100, 100, 0, "Paid", "Paid",
                DateTime.UtcNow, DateTime.UtcNow.AddDays(14),
                "A", null, "BK-1", null, null, null, DateTime.UtcNow, null, null, 0));

        var handler = new VoidInvoiceCommandHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new VoidInvoiceCommand(5), CancellationToken.None);

        result.Success.Should().BeFalse();
        repo.Verify(r => r.VoidAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void tenant_setup(out Mock<ITenantContext> tenant)
    {
        tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
    }
}
