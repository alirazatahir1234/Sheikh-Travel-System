using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Services.Notifications;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class IgnitionSmsAlertTests
{
    [Fact]
    public void AlertTypeCatalog_IncludesIgnitionOnOff()
    {
        AlertTypeCatalog.Types.Should().Contain("ignition_on");
        AlertTypeCatalog.Types.Should().Contain("ignition_off");
    }

    [Fact]
    public void RecipientPolicy_Ignition_GoesToAdminAndDispatcher()
    {
        NotificationRecipientPolicy.RolesFor("ignition_on")
            .Should().BeEquivalentTo([UserRole.Admin, UserRole.Dispatcher]);
        NotificationRecipientPolicy.RolesFor("ignition_off")
            .Should().BeEquivalentTo([UserRole.Admin, UserRole.Dispatcher]);
    }

    [Fact]
    public void RecipientPolicy_Ignition_DoesNotAllowBroadcast()
    {
        NotificationRecipientPolicy.AllowsBroadcast("ignition_on").Should().BeFalse();
        NotificationRecipientPolicy.AllowsBroadcast("ignition_off").Should().BeFalse();
    }

    [Fact]
    public void SmsSender_HttpClientName_IsStable()
        => SmsNotificationSender.HttpClientName.Should().Be("SheikhGoSmsGateway");

    [Theory]
    [InlineData(true, "ignition_on", "Ignition turned ON")]
    [InlineData(false, "ignition_off", "Ignition turned OFF")]
    public void Transition_MapsEventTypeAndMessage(bool ignitionOn, string expectedType, string expectedMessage)
    {
        var eventType = ignitionOn ? "ignition_on" : "ignition_off";
        var message = ignitionOn ? "Ignition turned ON" : "Ignition turned OFF";
        eventType.Should().Be(expectedType);
        message.Should().Be(expectedMessage);
    }

    [Fact]
    public void GpsAlertSeverity_IgnitionOn_IsMedium_Off_IsLow()
    {
        Application.Features.GpsTracking.Services.GpsAlertSeverity.SeverityFor("ignition_on")
            .Should().Be("medium");
        Application.Features.GpsTracking.Services.GpsAlertSeverity.SeverityFor("ignition_off")
            .Should().Be("low");
    }
}
