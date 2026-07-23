using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Tests.Tenants;

public class GetTenantModulesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCatalogModules_WhenConnectionFails()
    {
        var factory = new Mock<IDbConnectionFactory>();
        factory.Setup(f => f.CreateConnection()).Throws(new InvalidOperationException("unavailable"));

        var handler = new GetTenantModulesQueryHandler(factory.Object);
        var result = await handler.Handle(new GetTenantModulesQuery(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Count.Should().BeGreaterThan(0);
        result.Data.Should().Contain(m => m.Code == "DASHBOARD");
        result.Data.Should().OnlyContain(m => m.IsEnableable);
    }
}
