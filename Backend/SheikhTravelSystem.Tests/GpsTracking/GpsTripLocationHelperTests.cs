using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsTripLocationHelperTests
{
    [Fact]
    public void ResolveEndpointLabel_UsesCoordinatesWhenAddressMissing()
    {
        var label = GpsTripLocationHelper.ResolveEndpointLabel(null, 32.26261, 74.66339);
        label.Should().Be("32.26261, 74.66339");
    }

    [Fact]
    public void ResolveEndpointLabel_IgnoresUnknownPlaceholder()
    {
        var label = GpsTripLocationHelper.ResolveEndpointLabel("Unknown", 32.1, 74.3);
        label.Should().Be("32.10000, 74.30000");
    }

    [Fact]
    public void ApplyDisplayAddresses_FillsBothEndpoints()
    {
        var start = DateTime.UtcNow.AddHours(-1);
        var trip = new GpsTripDto(
            1,
            "Bus",
            null,
            start,
            start.AddMinutes(20),
            5.2,
            30m,
            45m,
            20,
            StartAddress: null,
            EndAddress: "unknown",
            StartLatitude: 32.1,
            StartLongitude: 74.3,
            EndLatitude: 32.2,
            EndLongitude: 74.4);

        var enriched = GpsTripLocationHelper.ApplyDisplayAddresses(trip);

        enriched.StartAddress.Should().Be("32.10000, 74.30000");
        enriched.EndAddress.Should().Be("32.20000, 74.40000");
    }
}
