using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Common;

public class TrafficRouteHelperTests
{
    [Theory]
    [InlineData(TripStatus.Enroute, true)]
    [InlineData(TripStatus.Delayed, true)]
    [InlineData(TripStatus.Started, false)]
    public void ShouldNavigateToDropoff(TripStatus status, bool expected)
    {
        TrafficRouteHelper.ShouldNavigateToDropoff(status).Should().Be(expected);
    }

    [Fact]
    public void TrafficLevel_heavy_when_duration_much_longer_than_static()
    {
        TrafficRouteHelper.TrafficLevel(1500, 1000).Should().Be("heavy");
        TrafficRouteHelper.TrafficLevel(1100, 1000).Should().Be("moderate");
        TrafficRouteHelper.TrafficLevel(1050, 1000).Should().Be("light");
    }
}
