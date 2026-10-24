using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Auth;

public class ConcurrentLoginLimitsTests
{
    [Fact]
    public void MaxActiveSessionsPerTenant_IsTwo()
    {
        ConcurrentLoginLimits.MaxActiveSessionsPerTenant.Should().Be(2);
        AuthSessionLimits.MaxActiveSessionsPerTenant.Should().Be(2);
    }

    [Fact]
    public void LimitReachedMessage_AsksToLogOutFromAnotherDevice()
    {
        ConcurrentLoginLimits.LimitReachedMessage.Should().Contain("Log out from another device");
        AuthSessionLimits.LimitReachedMessage.Should().Be(ConcurrentLoginLimits.LimitReachedMessage);
        ConcurrentLoginLimits.LimitReachedCode.Should().Be("SESSION_LIMIT_REACHED");
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("   ", null, false)]
    [InlineData("token", null, false)]
    public void HasActiveRefreshSession_MissingExpiry_IsInactive(string? token, DateTime? expiry, bool expected)
    {
        ConcurrentLoginLimits.HasActiveRefreshSession(token, expiry).Should().Be(expected);
    }

    [Fact]
    public void HasActiveRefreshSession_ValidFutureToken_IsActive()
    {
        ConcurrentLoginLimits.HasActiveRefreshSession("abc", DateTime.UtcNow.AddHours(1))
            .Should().BeTrue();
    }

    [Fact]
    public void HasActiveRefreshSession_ExpiredToken_IsInactive()
    {
        ConcurrentLoginLimits.HasActiveRefreshSession("abc", DateTime.UtcNow.AddMinutes(-1))
            .Should().BeFalse();
    }

    [Fact]
    public void HashRefreshToken_IsStableHex()
    {
        var a = SheikhTravelSystem.Infrastructure.Authentication.AuthSessionService.HashRefreshToken("abc");
        var b = SheikhTravelSystem.Infrastructure.Authentication.AuthSessionService.HashRefreshToken("abc");
        a.Should().Be(b);
        a.Should().HaveLength(64);
    }
}
