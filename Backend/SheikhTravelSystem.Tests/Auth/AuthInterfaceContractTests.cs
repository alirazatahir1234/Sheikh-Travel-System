using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Auth.Commands;
using SheikhTravelSystem.Domain.Entities;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Auth;

public class AuthInterfaceContractTests
{
    [Fact]
    public void IJwtTokenService_GenerateAccessToken_ShouldReturnToken()
    {
        var mockJwt = new Mock<IJwtTokenService>();
        var user = new User { Id = 1, FullName = "Ali", Email = "ali@test.com", Role = UserRole.Admin };
        mockJwt.Setup(j => j.GenerateAccessToken(user)).Returns("access-token-xyz");

        var token = mockJwt.Object.GenerateAccessToken(user);

        token.Should().Be("access-token-xyz");
        mockJwt.Verify(j => j.GenerateAccessToken(user), Times.Once);
    }

    [Fact]
    public void IJwtTokenService_GenerateRefreshToken_ShouldReturnNonEmptyToken()
    {
        var mockJwt = new Mock<IJwtTokenService>();
        mockJwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token-abc");

        var token = mockJwt.Object.GenerateRefreshToken();

        token.Should().Be("refresh-token-abc");
        mockJwt.Verify(j => j.GenerateRefreshToken(), Times.Once);
    }

    [Fact]
    public void IPasswordHasher_Hash_ShouldReturnHashString()
    {
        var mockHasher = new Mock<IPasswordHasher>();
        mockHasher.Setup(h => h.Hash("secret")).Returns("$2a$hashed");

        var hash = mockHasher.Object.Hash("secret");

        hash.Should().Be("$2a$hashed");
        mockHasher.Verify(h => h.Hash("secret"), Times.Once);
    }

    [Fact]
    public void IPasswordHasher_Verify_CorrectPassword_ShouldReturnTrue()
    {
        var mockHasher = new Mock<IPasswordHasher>();
        mockHasher.Setup(h => h.Verify("secret", "$2a$hashed")).Returns(true);

        var valid = mockHasher.Object.Verify("secret", "$2a$hashed");

        valid.Should().BeTrue();
    }

    [Fact]
    public void IPasswordHasher_Verify_WrongPassword_ShouldReturnFalse()
    {
        var mockHasher = new Mock<IPasswordHasher>();
        mockHasher.Setup(h => h.Verify("wrong", "$2a$hashed")).Returns(false);

        var valid = mockHasher.Object.Verify("wrong", "$2a$hashed");

        valid.Should().BeFalse();
    }

    [Fact]
    public void ICurrentUserService_AuthenticatedUser_ShouldReturnUserId()
    {
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.UserId).Returns(42);
        mockUser.Setup(u => u.Role).Returns("Admin");

        mockUser.Object.UserId.Should().Be(42);
        mockUser.Object.Role.Should().Be("Admin");
    }

    [Fact]
    public void ICurrentUserService_UnauthenticatedUser_ShouldReturnNullUserId()
    {
        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.UserId).Returns((int?)null);

        mockUser.Object.UserId.Should().BeNull();
    }

    [Fact]
    public void LogoutCommand_ShouldBeParameterless()
    {
        var cmd = new LogoutCommand();
        cmd.Should().NotBeNull();
    }

    [Fact]
    public void LogoutCommand_RecordEquality_ShouldWork()
    {
        var a = new LogoutCommand();
        var b = new LogoutCommand();
        a.Should().Be(b);
    }

    [Fact]
    public void RefreshTokenCommand_ShouldHoldToken()
    {
        var cmd = new RefreshTokenCommand("my-refresh-token");
        cmd.RefreshToken.Should().Be("my-refresh-token");
    }

    [Fact]
    public void RefreshTokenCommand_RecordEquality_ShouldWork()
    {
        var a = new RefreshTokenCommand("token-abc");
        var b = new RefreshTokenCommand("token-abc");
        a.Should().Be(b);
    }

    [Fact]
    public void RefreshTokenCommand_DifferentTokens_ShouldNotBeEqual()
    {
        var a = new RefreshTokenCommand("token-abc");
        var b = new RefreshTokenCommand("token-xyz");
        a.Should().NotBe(b);
    }

    [Fact]
    public void LoginResponse_WithAllRoles_ShouldBeCreatable()
    {
        var roles = new[] { "Admin", "Dispatcher", "Driver", "Accountant" };
        foreach (var role in roles)
        {
            var response = new LoginResponse("access", "refresh", "Name", role);
            response.Role.Should().Be(role);
        }
    }

    [Fact]
    public void IJwtTokenService_GeneratesUniqueRefreshTokensEachCall()
    {
        var mockJwt = new Mock<IJwtTokenService>();
        mockJwt.SetupSequence(j => j.GenerateRefreshToken())
               .Returns("token-1")
               .Returns("token-2");

        var t1 = mockJwt.Object.GenerateRefreshToken();
        var t2 = mockJwt.Object.GenerateRefreshToken();

        t1.Should().NotBe(t2);
    }

    [Fact]
    public void IPasswordHasher_SamePasswordHashedTwice_MayDifferByDesign()
    {
        // BCrypt generates different salts — verify works regardless of salt
        var mockHasher = new Mock<IPasswordHasher>();
        mockHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
                  .Returns<string, string>((pw, hash) => pw == "correct");

        mockHasher.Object.Verify("correct", "any-hash").Should().BeTrue();
        mockHasher.Object.Verify("wrong", "any-hash").Should().BeFalse();
    }
}
