using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Settlements.Commands;
using SheikhTravelSystem.Application.Features.Settlements.DTOs;

namespace SheikhTravelSystem.Tests.Settlements;

public class GenerateDraftSettlementCommandTests
{
    [Fact]
    public async Task Handle_computes_payable_and_passes_tenantId()
    {
        var repo = new Mock<IDriverSettlementRepository>();
        var tenant = new Mock<ITenantContext>();
        var user = new Mock<ICurrentUserService>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(4);
        user.SetupGet(u => u.UserId).Returns(8);

        var start = new DateTime(2026, 10, 1);
        var end = new DateTime(2026, 10, 15);
        var totals = new SettlementTotals
        {
            GrossCollected = 1000m,
            FuelDeducted = 200m,
            AllowanceAmount = 50m
        };

        repo.Setup(r => r.DriverExistsAsync(12, 4, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.CalculateTotalsAsync(4, 12, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(totals);
        repo.Setup(r => r.BuildLinesAsync(4, 12, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SettlementLineInput>());
        repo.Setup(r => r.CreateDraftAsync(
                4, 8, 12, start, end, totals, 10m, 860m,
                It.IsAny<IReadOnlyList<SettlementLineInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);

        var handler = new GenerateDraftSettlementCommandHandler(repo.Object, tenant.Object, user.Object);
        var result = await handler.Handle(
            new GenerateDraftSettlementCommand(12, start, end, 10m), CancellationToken.None);

        // payable = 1000 - 200 + 50 + 10 = 860
        result.Success.Should().BeTrue();
        result.Data.Should().Be(99);
        repo.Verify(r => r.CreateDraftAsync(
            4, 8, 12, start, end, totals, 10m, 860m,
            It.IsAny<IReadOnlyList<SettlementLineInput>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void DriverSettlementRepository_source_contains_tenant_guards()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Backend", "SheikhTravelSystem.Infrastructure",
            "Persistence", "Repositories", "DriverSettlementRepository.cs");
        var text = File.ReadAllText(path);
        text.Should().Contain("s.TenantId = @TenantId");
        text.Should().Contain("INSERT INTO DriverSettlements");
        text.Should().Contain("WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0");
        text.Should().Contain("b.DriverId = @DriverId");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Backend")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repo root not found");
    }
}
