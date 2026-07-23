using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Tests.Platform;

public class DatabaseResetServiceTests
{
    [Fact]
    public async Task ResetAsync_Throws_Outside_Development_Or_Staging()
    {
        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);

        var service = new DatabaseResetService(
            Mock.Of<IDbConnectionFactory>(),
            Mock.Of<IDatabaseSeeder>(),
            Mock.Of<ITenantRoleSeedService>(),
            Mock.Of<IPasswordHasher>(),
            env.Object,
            NullLogger<DatabaseResetService>.Instance);

        var act = () => service.ResetAsync(1, "127.0.0.1", "test", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Development or Staging*");
    }
}
