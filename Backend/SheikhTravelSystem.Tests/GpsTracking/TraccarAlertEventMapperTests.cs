using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class TraccarAlertEventMapperTests
{
    [Theory]
    [InlineData("ignitionOn", "ignition_on")]
    [InlineData("ignitionOff", "ignition_off")]
    [InlineData("geofenceEnter", "geofence_enter")]
    [InlineData("geofenceExit", "geofence_exit")]
    [InlineData("deviceOnline", "online")]
    [InlineData("deviceOffline", "vehicle_offline")]
    [InlineData("ignitionon", "ignition_on")]
    [InlineData("deviceoffline", "vehicle_offline")]
    public void MapEventType_CanonicalizesTraccarTypes(string input, string expected)
        => TraccarAlertEventMapper.MapEventType(input).Should().Be(expected);

    [Theory]
    [InlineData("deviceMoving")]
    [InlineData("deviceStopped")]
    [InlineData("queuedCommandSent")]
    [InlineData("commandResult")]
    public void MapEventType_SkipsNoise(string input)
        => TraccarAlertEventMapper.MapEventType(input).Should().BeNull();

    [Fact]
    public void ExpandFilterAliases_OfflineChip_IncludesVehicleOffline()
    {
        var aliases = TraccarAlertEventMapper.ExpandFilterAliases("offline");
        aliases.Should().Contain("offline");
        aliases.Should().Contain("vehicle_offline");
        aliases.Should().Contain("device_offline");
        aliases.Should().Contain("gps_offline");
    }

    [Fact]
    public void ExpandFilterAliases_IgnitionOn_IncludesHistoricalMisspelling()
    {
        var aliases = TraccarAlertEventMapper.ExpandFilterAliases("ignition_on");
        aliases.Should().Contain("ignition_on");
        aliases.Should().Contain("ignitionon");
    }

    [Fact]
    public void FormatMessage_MatchesReconnectCopy()
        => TraccarAlertEventMapper.FormatMessage("online")
            .Should().Be("Vehicle has reconnected to the tracking server.");

    [Fact]
    public void GpsEventTypeNormalizer_CollapsesOfflineFamily()
        => GpsEventTypeNormalizer.Normalize("vehicle_offline").Should().Be("offline");
}
