using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Invoices.Commands;
using SheikhTravelSystem.Application.Features.Invoices.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Invoices;

public class IssueFromBookingCommandTests
{
    [Fact]
    public async Task Handle_passes_tenantId_and_creates_issued_invoice()
    {
        var repo = new Mock<ICustomerInvoiceRepository>();
        var tenant = new Mock<ITenantContext>();
        var user = new Mock<ICurrentUserService>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(9);
        user.SetupGet(u => u.UserId).Returns(3);

        repo.Setup(r => r.GetBookingSnapshotAsync(11, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceBookingSnapshot
            {
                BookingId = 11,
                CustomerId = 5,
                TotalAmount = 1000m,
                BookingNumber = "BK-1",
                CustomerName = "Ali",
                RouteName = "A -> B",
                Status = (int)BookingStatus.Confirmed
            });
        repo.Setup(r => r.HasActiveInvoiceForBookingAsync(11, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.GetTotalPaidForBookingAsync(11, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(250m);
        repo.Setup(r => r.AllocateInvoiceNumberAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync("INV-2026-0001");
        repo.Setup(r => r.CreateAsync(
                9, 3, It.IsAny<InvoiceBookingSnapshot>(), "INV-2026-0001", 250m, 750m,
                InvoiceStatus.Issued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var handler = new IssueFromBookingCommandHandler(repo.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new IssueFromBookingCommand(11), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        repo.Verify(r => r.CreateAsync(
            9, 3, It.IsAny<InvoiceBookingSnapshot>(), "INV-2026-0001", 250m, 750m,
            InvoiceStatus.Issued, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_marks_paid_when_balance_is_zero()
    {
        var repo = new Mock<ICustomerInvoiceRepository>();
        var tenant = new Mock<ITenantContext>();
        var user = new Mock<ICurrentUserService>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        user.SetupGet(u => u.UserId).Returns(1);

        repo.Setup(r => r.GetBookingSnapshotAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvoiceBookingSnapshot
            {
                BookingId = 2,
                CustomerId = 1,
                TotalAmount = 500m,
                Status = (int)BookingStatus.Completed
            });
        repo.Setup(r => r.HasActiveInvoiceForBookingAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.GetTotalPaidForBookingAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(500m);
        repo.Setup(r => r.AllocateInvoiceNumberAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("INV-1");
        repo.Setup(r => r.CreateAsync(
                1, 1, It.IsAny<InvoiceBookingSnapshot>(), "INV-1", 500m, 0m,
                InvoiceStatus.Paid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var handler = new IssueFromBookingCommandHandler(repo.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new IssueFromBookingCommand(2), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.CreateAsync(
            1, 1, It.IsAny<InvoiceBookingSnapshot>(), "INV-1", 500m, 0m,
            InvoiceStatus.Paid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CustomerInvoiceRepository_source_contains_tenant_guards()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Backend", "SheikhTravelSystem.Infrastructure",
            "Persistence", "Repositories", "CustomerInvoiceRepository.cs");
        var text = File.ReadAllText(path);
        text.Should().Contain("TenantId = @TenantId");
        text.Should().Contain("INSERT INTO CustomerInvoices");
        text.Should().Contain("WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Sheikh-Travel-System.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "Backend")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repo root not found");
    }
}
