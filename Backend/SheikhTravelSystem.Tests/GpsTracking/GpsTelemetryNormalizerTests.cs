using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GpsTelemetryNormalizerTests
{
    [Fact]
    public void Acc_on_at_rest_stays_on_even_when_motion_false()
    {
        // Idle with key ON: ACC true must not be cleared by motion=false.
        var ignition = GpsTelemetryNormalizer.ResolveIgnition(
            ignition: true,
            motion: false,
            speedKmh: 0);

        Assert.True(ignition);
    }

    [Fact]
    public void Acc_off_stays_off()
    {
        Assert.False(GpsTelemetryNormalizer.ResolveIgnition(false, false, 0));
        Assert.False(GpsTelemetryNormalizer.ResolveIgnition(false, true, 5));
    }

    [Fact]
    public void Acc_omitted_with_motion_false_at_rest_is_off()
    {
        // Command-result / sparse packets omit ignition — do not leave sticky ON.
        var ignition = GpsTelemetryNormalizer.ResolveIgnition(
            ignition: null,
            motion: false,
            speedKmh: 0);

        Assert.False(ignition);
    }

    [Fact]
    public void Acc_omitted_without_motion_stays_unknown()
    {
        Assert.Null(GpsTelemetryNormalizer.ResolveIgnition(null, null, 0));
        Assert.Null(GpsTelemetryNormalizer.ResolveIgnition(null, true, 0));
    }

    [Fact]
    public void Acc_on_while_moving_stays_on()
    {
        Assert.True(GpsTelemetryNormalizer.ResolveIgnition(true, true, 40));
        Assert.True(GpsTelemetryNormalizer.ResolveIgnition(true, false, 40));
    }
}
