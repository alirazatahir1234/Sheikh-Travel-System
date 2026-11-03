using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class TraccarDeviceLinkResolverTests
{
    [Fact]
    public void PreferCanonical_duplicate_traccar_id_prefers_vehicle_linked()
    {
        var rows = new[]
        {
            new TraccarDeviceLinkResolver.DeviceLink(19, GpsDeviceId: 2003, VehicleId: null, LastSeenAt: DateTime.UtcNow),
            new TraccarDeviceLinkResolver.DeviceLink(19, GpsDeviceId: 1, VehicleId: 2, LastSeenAt: DateTime.UtcNow.AddHours(-1)),
        };

        var map = TraccarDeviceLinkResolver.PreferCanonical(rows);

        map.Should().HaveCount(1);
        map[19].GpsDeviceId.Should().Be(1);
        map[19].VehicleId.Should().Be(2);
    }

    [Fact]
    public void PreferCanonical_duplicate_without_vehicle_prefers_freshest_last_seen()
    {
        var newer = DateTime.UtcNow;
        var older = newer.AddHours(-2);
        var rows = new[]
        {
            new TraccarDeviceLinkResolver.DeviceLink(19, GpsDeviceId: 10, VehicleId: null, LastSeenAt: older),
            new TraccarDeviceLinkResolver.DeviceLink(19, GpsDeviceId: 11, VehicleId: null, LastSeenAt: newer),
        };

        var map = TraccarDeviceLinkResolver.PreferCanonical(rows);

        map[19].GpsDeviceId.Should().Be(11);
    }

    [Fact]
    public void PreferCanonical_does_not_throw_on_duplicates()
    {
        var act = () => TraccarDeviceLinkResolver.PreferCanonical(
        [
            new TraccarDeviceLinkResolver.DeviceLink(19, 1, 2, DateTime.UtcNow),
            new TraccarDeviceLinkResolver.DeviceLink(19, 2003, null, DateTime.UtcNow),
            new TraccarDeviceLinkResolver.DeviceLink(23, 2002, 2005, DateTime.UtcNow),
        ]);

        act.Should().NotThrow();
        var map = act();
        map.Should().HaveCount(2);
        map.Keys.Should().BeEquivalentTo([19, 23]);
    }
}
