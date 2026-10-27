using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.SosDispatch;

public class GeoDistanceTests
{
    [Theory]
    [InlineData(25.2048, 55.2708, "AE")]
    [InlineData(31.5204, 74.3587, "PK")]
    [InlineData(24.7136, 46.6753, "SA")]
    public void InferRegionCode_KnownCities(double lat, double lng, string expected)
    {
        GeoDistance.InferRegionCode(lat, lng).Should().Be(expected);
    }

    [Fact]
    public void HaversineKm_SamePoint_IsZero()
    {
        GeoDistance.HaversineKm(31.5, 74.3, 31.5, 74.3).Should().Be(0);
    }
}
