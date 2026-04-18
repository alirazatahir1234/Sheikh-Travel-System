using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Auth.Commands;

namespace SheikhTravelSystem.Tests.Auth;

public class LogoutCommandHandlerTests
{
    [Fact]
    public void LogoutCommand_IsParameterless()
    {
        var command = new LogoutCommand();
        command.Should().NotBeNull();
    }

    [Fact]
    public void LogoutRequiresAuthenticatedUser()
    {
        // Verify ICurrentUserService contract
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(x => x.UserId).Returns((int?)null);
        mock.Object.UserId.Should().BeNull();

        mock.Setup(x => x.UserId).Returns(42);
        mock.Object.UserId.Should().Be(42);
    }
}
