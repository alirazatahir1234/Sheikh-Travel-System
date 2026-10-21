using FluentAssertions;
using SheikhTravelSystem.Application.Features.Drivers.Services;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using Xunit;

namespace SheikhTravelSystem.Tests.Drivers;

public class DriverTelematicsEnforcementPolicyTests
{
    [Theory]
    [InlineData("harsh_braking", true)]
    [InlineData("harsh_acceleration", true)]
    [InlineData("speed_exceeded", true)]
    [InlineData("overspeed", true)]
    [InlineData("geofence_enter", false)]
    public void ShouldCreateViolation_matches_enforced_types(string eventType, bool expected)
        => DriverTelematicsEnforcementPolicy.ShouldCreateViolation(eventType).Should().Be(expected);

    [Fact]
    public void Suspend_threshold_is_three_harsh_in_seven_days()
    {
        DriverTelematicsEnforcementPolicy.HarshViolationSuspendThreshold.Should().Be(3);
        DriverTelematicsEnforcementPolicy.RollingWindowDays.Should().Be(7);
        DriverTelematicsEnforcementPolicy.CountsTowardSuspend("HarshBraking").Should().BeTrue();
        DriverTelematicsEnforcementPolicy.CountsTowardSuspend("Overspeed").Should().BeFalse();
    }

    [Theory]
    [InlineData("hardBraking", null, "harsh_braking")]
    [InlineData("harshAcceleration", null, "harsh_acceleration")]
    [InlineData("alarm", "hardBraking", "harsh_braking")]
    [InlineData("alarm", "sos", null)]
    [InlineData("deviceOnline", null, null)]
    public void TraccarHarshEventMapper_maps_canonical(string type, string? alarm, string? expected)
        => TraccarHarshEventMapper.TryMapCanonical(type, alarm).Should().Be(expected);
}
