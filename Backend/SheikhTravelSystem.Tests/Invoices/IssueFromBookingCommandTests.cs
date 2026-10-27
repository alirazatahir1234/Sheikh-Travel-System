using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Invoices.Commands;
using SheikhTravelSystem.Application.Features.Invoices.Services;

namespace SheikhTravelSystem.Tests.Invoices;

public class IssueFromBookingCommandTests
{
    [Fact]
    public async Task Handle_delegates_to_invoice_issue_service()
    {
        var issue = new Mock<IInvoiceIssueService>();
        var tenant = new Mock<ITenantContext>();
        var user = new Mock<ICurrentUserService>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(9);
        user.SetupGet(u => u.UserId).Returns(3);
        issue.Setup(s => s.IssueFromBookingAsync(9, 3, 11, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<int>.SuccessResponse(42));

        var handler = new IssueFromBookingCommandHandler(issue.Object, tenant.Object, user.Object);
        var result = await handler.Handle(new IssueFromBookingCommand(11), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        issue.Verify(s => s.IssueFromBookingAsync(9, 3, 11, It.IsAny<CancellationToken>()), Times.Once);
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
