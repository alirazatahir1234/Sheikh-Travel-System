using FluentAssertions;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Tests.Tenants;

public class GetTenantModulesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCatalogModules()
    {
        var handler = new GetTenantModulesQueryHandler();
        var result = await handler.Handle(new GetTenantModulesQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count.Should().BeGreaterThan(0);
        result.Data.Should().Contain(m => m.Code == "DASHBOARD");
    }
}
