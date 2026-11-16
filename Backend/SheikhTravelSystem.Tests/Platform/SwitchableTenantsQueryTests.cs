using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Common.Multitenancy;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Tests.Platform;

public class SwitchableTenantsQueryTests
{
    [Fact]
    public async Task Handler_includes_suspended_and_expired_with_status_flags()
    {
        var repo = new Mock<IPlatformRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetSwitchableTenantsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SwitchableTenantRow(1, "Active Co", "active", "A1", true, "Active", "Active"),
                new SwitchableTenantRow(2, "Suspended Co", "susp", "S1", false, "Suspended", "Suspended"),
                new SwitchableTenantRow(3, "Expired Co", "exp", "E1", false, "Active", "Expired"),
                new SwitchableTenantRow(4, "Deactivated Co", "dea", "D1", false, "Active", "Active"),
            ]);

        var handler = new GetSwitchableTenantsQueryHandler(repo.Object);
        var result = await handler.Handle(new GetSwitchableTenantsQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Should().HaveCount(4);

        result.Data.Should().Contain(t =>
            t.Name == "Suspended Co" && t.Status == SwitchableTenantStatus.Suspended);
        result.Data.Should().Contain(t =>
            t.Name == "Expired Co" && t.Status == SwitchableTenantStatus.Expired);
        result.Data.Should().Contain(t =>
            t.Name == "Deactivated Co" && t.Status == SwitchableTenantStatus.Deactivated);
        result.Data.Should().Contain(t =>
            t.Name == "Active Co" && t.Status == SwitchableTenantStatus.Active);
    }
}
