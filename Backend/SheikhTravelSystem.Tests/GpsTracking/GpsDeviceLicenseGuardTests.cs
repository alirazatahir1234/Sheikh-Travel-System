using FluentAssertions;
using SheikhTravelSystem.Infrastructure.Persistence;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsDeviceLicenseGuardTests
{
    [Theory]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(10, 9, false)]
    [InlineData(10, 12, true)]
    [InlineData(null, 500, false)]
    public void ExceedsLicense_blocks_when_all_licensed_devices_are_used(int? licensed, int used, bool expected)
        => GpsDeviceLicenseGuard.ExceedsLicense(licensed, used).Should().Be(expected);
}
