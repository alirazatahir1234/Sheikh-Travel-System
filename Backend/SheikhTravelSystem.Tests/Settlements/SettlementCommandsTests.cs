using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Settlements.Commands;
using SheikhTravelSystem.Application.Features.Settlements.DTOs;

namespace SheikhTravelSystem.Tests.Settlements;

public class SettlementCommandsTests
{
    [Fact]
    public async Task GenerateDraft_computes_payable_and_creates()
    {
        var repo = new Mock<IDriverSettlementRepository>();
        var tenant = new Mock<ITenantContext>();
        var user = new Mock<ICurrentUserService>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(2);
        user.Setup(u => u.UserId).Returns(3);

        var start = new DateTime(2026, 10, 1);
        var end = new DateTime(2026, 10, 7);
        repo.Setup(r => r.DriverExistsAsync(8, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.CalculateTotalsAsync(2, 8, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettlementTotals
            {
                GrossCollected = 5000,
                FuelDeducted = 800,
                AllowanceAmount = 200
            });
        repo.Setup(r => r.BuildLinesAsync(2, 8, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SettlementLineInput>());
        repo.Setup(r => r.CreateDraftAsync(
                2, 3, 8, start, end,
                It.IsAny<SettlementTotals>(), 50m, 4450m,
                It.IsAny<IReadOnlyList<SettlementLineInput>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);

        var handler = new GenerateDraftSettlementCommandHandler(repo.Object, tenant.Object, user.Object);
        var result = await handler.Handle(
            new GenerateDraftSettlementCommand(8, start, end, Adjustments: 50),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(99);
        // payable = 5000 - 800 + 200 + 50 = 4450
        repo.Verify(r => r.CreateDraftAsync(
            2, 3, 8, start, end,
            It.IsAny<SettlementTotals>(), 50m, 4450m,
            It.IsAny<IReadOnlyList<SettlementLineInput>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_rejects_non_draft()
    {
        var repo = new Mock<IDriverSettlementRepository>();
        var tenant = new Mock<ITenantContext>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.GetHeaderAsync(4, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettlementDetailDto(
                4, 1, "Drv", DateTime.UtcNow.Date, DateTime.UtcNow.Date,
                100, 0, 0, 0, 100, "Paid", DateTime.UtcNow, "ref", null,
                DateTime.UtcNow, null, Array.Empty<SettlementLineDto>()));

        var handler = new ApproveSettlementCommandHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new ApproveSettlementCommand(4), CancellationToken.None);

        result.Success.Should().BeFalse();
        repo.Verify(r => r.ApproveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
