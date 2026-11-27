using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Application.Features.Notifications;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GeofenceNotifyChannelsTests
{
    [Fact]
    public void Parse_keeps_known_channels_in_canonical_spelling()
    {
        GeofenceNotifyChannels.Parse("inapp, WHATSAPP,Fax,email,inapp")
            .Should().Equal(GeofenceNotifyChannels.InApp, GeofenceNotifyChannels.WhatsApp, GeofenceNotifyChannels.Email);
        GeofenceNotifyChannels.Parse(null).Should().BeEmpty();
    }

    [Fact]
    public void Format_returns_null_when_nothing_valid_was_chosen()
    {
        GeofenceNotifyChannels.Format(["Fax"]).Should().BeNull();
        GeofenceNotifyChannels.Format(["sms", "DriverPush"]).Should().Be("Sms,DriverPush");
    }

    [Fact]
    public void In_app_also_lights_the_browser_and_driver_push_is_not_a_recipient_channel()
    {
        GeofenceNotifyChannels.ToDeliveryChannels([GeofenceNotifyChannels.InApp, GeofenceNotifyChannels.DriverPush])
            .Should().Equal(NotificationChannels.InApp, NotificationChannels.Browser);
        GeofenceNotifyChannels.ToDeliveryChannels([GeofenceNotifyChannels.Sms, GeofenceNotifyChannels.Push])
            .Should().Equal(NotificationChannels.Sms, NotificationChannels.Push);
    }
}
