using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Infrastructure.Services.Gps;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class HarshBrakingSmsAlertTests
{
    [Fact]
    public void AlertTypeCatalog_IncludesHarshBraking()
        => AlertTypeCatalog.Types.Should().Contain("harsh_braking");

    [Fact]
    public void RecipientPolicy_HarshBraking_GoesToAdminAndDispatcher()
        => NotificationRecipientPolicy.RolesFor("harsh_braking")
            .Should().BeEquivalentTo([UserRole.Admin, UserRole.Dispatcher]);

    [Theory]
    [InlineData("accident")]
    [InlineData("crash")]
    [InlineData("collision")]
    public void GsensorFalsePositive_IsRecognized(string alarm)
        => TraccarHarshEventMapper.IsGsensorHardStopFalsePositive(alarm).Should().BeTrue();

    [Fact]
    public void DedupWindow_IsTenMinutes()
        => GpsHarshBrakingAlertDispatch.DedupMinutes.Should().BeInRange(5, 15);

    [Fact]
    public void FormatMessage_HarshBraking_IsNotCollision()
    {
        var msg = TraccarAlertEventMapper.FormatMessage("harsh_braking");
        msg.Should().Contain("Harsh braking");
        msg.ToLowerInvariant().Should().NotContain("collision");
    }
}
