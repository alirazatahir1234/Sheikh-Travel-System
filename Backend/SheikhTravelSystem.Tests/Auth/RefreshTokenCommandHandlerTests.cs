using FluentAssertions;
using SheikhTravelSystem.Application.Features.Auth.Commands;

namespace SheikhTravelSystem.Tests.Auth;

public class RefreshTokenCommandHandlerTests
{
    [Fact]
    public void RefreshTokenCommand_HoldsToken()
    {
        var command = new RefreshTokenCommand("some-refresh-token");
        command.RefreshToken.Should().Be("some-refresh-token");
    }

    [Fact]
    public void LoginResponse_Record_Equality()
    {
        var a = new LoginResponse("at", "rt", "Name", "Admin");
        var b = new LoginResponse("at", "rt", "Name", "Admin");
        a.Should().Be(b);
    }

    [Fact]
    public void LoginResponse_Record_Inequality()
    {
        var a = new LoginResponse("at1", "rt", "Name", "Admin");
        var b = new LoginResponse("at2", "rt", "Name", "Admin");
        a.Should().NotBe(b);
    }
}
