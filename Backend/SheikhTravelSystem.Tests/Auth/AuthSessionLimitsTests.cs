using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Auth;

public class AuthSessionLimitsTests
{
    [Fact]
    public void TenantWideCap_IsDisabled_ZeroMeansUnlimited()
    {
        // 0 = unlimited; AuthSessionService skips the BLOCK check.
        AuthSessionLimits.MaxActiveSessionsPerTenant.Should().Be(0);
        AuthSessionLimits.LimitReachedMessage
            .Should().Contain("maximum active logins")
            .And.Contain("Log out from another device");
        AuthSessionLimits.LimitReachedCode.Should().Be("SESSION_LIMIT_REACHED");
        AuthSessionLimits.ClaimSessionId.Should().Be("sid");
    }
}
