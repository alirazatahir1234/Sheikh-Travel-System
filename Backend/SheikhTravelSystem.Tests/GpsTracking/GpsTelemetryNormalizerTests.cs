using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsTelemetryNormalizerTests
{
    [Fact]
    public void Motion_false_at_rest_forces_ignition_off()
    {
        var ignition = GpsTelemetryNormalizer.ResolveIgnition(
            ignition: true,
            motion: false,
            speedKmh: 0);

        Assert.False(ignition);
    }

    [Fact]
    public void Motion_true_keeps_reported_ignition_on()
    {
        var ignition = GpsTelemetryNormalizer.ResolveIgnition(
            ignition: true,
            motion: true,
            speedKmh: 0);

        Assert.True(ignition);
    }

    [Fact]
    public void Motion_null_keeps_reported_ignition()
    {
        Assert.True(GpsTelemetryNormalizer.ResolveIgnition(true, null, 0));
        Assert.False(GpsTelemetryNormalizer.ResolveIgnition(false, null, 0));
        Assert.Null(GpsTelemetryNormalizer.ResolveIgnition(null, null, 0));
    }

    [Fact]
    public void Motion_false_while_moving_does_not_clear_ignition()
    {
        var ignition = GpsTelemetryNormalizer.ResolveIgnition(
            ignition: true,
            motion: false,
            speedKmh: 40);

        Assert.True(ignition);
    }
}
