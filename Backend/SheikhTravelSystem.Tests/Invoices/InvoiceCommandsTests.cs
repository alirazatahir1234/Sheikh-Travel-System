using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices.Commands;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Invoices;

public class InvoiceCommandsTests
{
    [Fact]
    public async Task IssueFromBooking_creates_issued_invoice_with_balance()
    {
        var repo = new Mock<ICustomerInvoiceRepository>();
        var tenant = new Mock<ITenantContext>();
        var user = new Mock<ICurrentUserService>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        user.Setup(u => u.UserId).Returns(9);

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
                1, 9, It.IsAny<InvoiceBookingSnapshot>(), "INV-2026-0001",
                250m, 750m, InvoiceStatus.Issued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);

        var handler = new IssueFromBookingCommandHandler(repo.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new IssueFromBookingCommand(42), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(11);
        repo.Verify(r => r.CreateAsync(
            1, 9, It.IsAny<InvoiceBookingSnapshot>(), "INV-2026-0001",
            250m, 750m, InvoiceStatus.Issued, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Void_rejects_paid_invoice()
    {
        var repo = new Mock<ICustomerInvoiceRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetByIdAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceDetailDto(
                5, 1, 1, "INV-1", 100, 100, 0, "Paid", DateTime.UtcNow,
                "A", "BK-1", null, null, DateTime.UtcNow, null));

        var handler = new VoidInvoiceCommandHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new VoidInvoiceCommand(5), CancellationToken.None);

        result.Success.Should().BeFalse();
        repo.Verify(r => r.VoidAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
