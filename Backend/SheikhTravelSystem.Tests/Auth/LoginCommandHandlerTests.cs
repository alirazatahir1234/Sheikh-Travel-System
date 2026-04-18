using FluentAssertions;
using FluentValidation;
using SheikhTravelSystem.Application.Features.Auth.Commands;

namespace SheikhTravelSystem.Tests.Auth;

public class LoginCommandHandlerTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_EmptyEmail_ShouldFail()
    {
        var command = new LoginCommand("", "password");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Email"));
    }

    [Fact]
    public void Validate_InvalidEmailFormat_ShouldFail()
    {
        var command = new LoginCommand("not-an-email", "password");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyPassword_ShouldFail()
    {
        var command = new LoginCommand("test@test.com", "");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidInput_ShouldPass()
    {
        var command = new LoginCommand("test@test.com", "Password123");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void LoginResponse_ShouldHoldTokenData()
    {
        var response = new LoginResponse("access", "refresh", "Ali", "Admin");
        response.AccessToken.Should().Be("access");
        response.RefreshToken.Should().Be("refresh");
        response.FullName.Should().Be("Ali");
        response.Role.Should().Be("Admin");
    }

    [Fact]
    public void LoginCommand_ShouldHoldCredentials()
    {
        var command = new LoginCommand("user@test.com", "pass123");
        command.Email.Should().Be("user@test.com");
        command.Password.Should().Be("pass123");
    }
}
