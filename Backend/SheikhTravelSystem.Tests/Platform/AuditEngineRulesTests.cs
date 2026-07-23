using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Platform;

public class AuditEngineRulesTests
{
    [Theory]
    [InlineData(20, false, 20)]
    [InlineData(200, false, 200)]
    [InlineData(500, false, 200)]
    [InlineData(0, false, 1)]
    [InlineData(5000, true, 5000)]
    [InlineData(20_000, true, 10_000)]
    public void ClampPageSize_Respects_List_And_Export_Caps(int requested, bool forExport, int expected)
    {
        AuditEngineRules.ClampPageSize(requested, forExport).Should().Be(expected);
    }

    [Theory]
    [InlineData("auth.login.success", false, true)]
    [InlineData("auth.login.failed", false, true)]
    [InlineData("auth.logout", false, true)]
    [InlineData("auth.lockout", false, true)]
    [InlineData("auth.password.changed", false, true)]
    [InlineData("user.create", false, false)]
    [InlineData("auth.lockout", true, false)]
    [InlineData("auth.login.failed", true, false)]
    public void ShouldSkipAuthEvent_When_LoginEvents_Disabled(string eventKey, bool loginEvents, bool skip)
    {
        AuditEngineRules.ShouldSkipAuthEvent(eventKey, loginEvents).Should().Be(skip);
    }

    [Fact]
    public void ResolveCommandSuccess_Reads_ApiResponse_Flag()
    {
        AuditEngineRules.ResolveCommandSuccess(ApiResponse<int>.SuccessResponse(1))
            .Should().BeTrue();
        AuditEngineRules.ResolveCommandSuccess(ApiResponse<int>.FailResponse("nope"))
            .Should().BeFalse();
        AuditEngineRules.ResolveCommandSuccess("plain").Should().BeTrue();
        AuditEngineRules.ResolveCommandSuccess(null).Should().BeTrue();
    }
}
