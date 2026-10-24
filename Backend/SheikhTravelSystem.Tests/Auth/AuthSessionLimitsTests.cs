using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Auth;

public class AuthSessionLimitsTests
{
    [Fact]
    public void TenantWideCap_IsTwo_WithBlockSemantics()
    {
        AuthSessionLimits.MaxActiveSessionsPerTenant.Should().Be(2);
        AuthSessionLimits.LimitReachedMessage
            .Should().Contain("2 active logins")
            .And.Contain("Log out from another device");
        AuthSessionLimits.LimitReachedCode.Should().Be("SESSION_LIMIT_REACHED");
        AuthSessionLimits.ClaimSessionId.Should().Be("sid");
    }
}
