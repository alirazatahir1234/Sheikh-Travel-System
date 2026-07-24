using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class TraccarAdaptiveIntervalTests
{
    private static TraccarOptions Options() => new()
    {
        AdaptivePositionSync = true,
        MovingSpeedKmh = 10,
        MovingIntervalSeconds = 5,
        SlowTrafficIntervalSeconds = 15,
        IdleIntervalSeconds = 30,
        ParkedIntervalSeconds = 300,
        PositionSyncIntervalSeconds = 5,
        SosAlarmValues = ["sos", "panic"]
    };

    [Fact]
    public void Empty_fleet_resolves_to_Parked()
    {
        var r = TraccarAdaptiveInterval.Resolve([], Options());
        r.IntervalSeconds.Should().Be(300);
        r.Reason.Should().Be(TraccarAdaptiveInterval.ReasonParked);
    }

    [Fact]
    public void Moving_vehicle_uses_moving_interval()
    {
        var r = TraccarAdaptiveInterval.Resolve(
            [new TraccarAdaptiveInterval.Sample(45m, true, null)],
            Options());
        r.IntervalSeconds.Should().Be(5);
        r.Reason.Should().Be(TraccarAdaptiveInterval.ReasonMoving);
    }

    [Fact]
    public void Slow_traffic_uses_slow_interval()
    {
        var r = TraccarAdaptiveInterval.Resolve(
            [new TraccarAdaptiveInterval.Sample(6m, true, null)],
            Options());
        r.IntervalSeconds.Should().Be(15);
        r.Reason.Should().Be(TraccarAdaptiveInterval.ReasonSlowTraffic);
    }

    [Fact]
    public void Idle_ignition_on_uses_idle_interval()
    {
        var r = TraccarAdaptiveInterval.Resolve(
            [new TraccarAdaptiveInterval.Sample(0m, true, null)],
            Options());
        r.IntervalSeconds.Should().Be(30);
        r.Reason.Should().Be(TraccarAdaptiveInterval.ReasonIdle);
    }

    [Fact]
    public void Parked_ignition_off_uses_parked_interval()
    {
        var r = TraccarAdaptiveInterval.Resolve(
            [new TraccarAdaptiveInterval.Sample(0m, false, null)],
            Options());
        r.IntervalSeconds.Should().Be(300);
        r.Reason.Should().Be(TraccarAdaptiveInterval.ReasonParked);
    }

    [Fact]
    public void Sos_forces_moving_interval()
    {
        var r = TraccarAdaptiveInterval.Resolve(
            [new TraccarAdaptiveInterval.Sample(0m, false, "sos")],
            Options());
        r.IntervalSeconds.Should().Be(5);
        r.Reason.Should().Be(TraccarAdaptiveInterval.ReasonSos);
    }
}
