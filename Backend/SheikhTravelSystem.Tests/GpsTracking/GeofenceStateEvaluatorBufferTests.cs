using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>Each geofence's GPS tolerance buffer replaces the fixed 25 m band.</summary>
public class GeofenceStateEvaluatorBufferTests
{
    private static readonly DateTime T0 = new(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);

    private static GeofenceTransition Cross(double distance, double buffer)
    {
        var state = new GeofenceZoneState(false, T0);
        var first = GeofenceStateEvaluator.Evaluate(state, true, distance, T0.AddSeconds(15), buffer);
        var second = GeofenceStateEvaluator.Evaluate(first.State, true, distance, T0.AddSeconds(30), buffer);
        return second.Transition;
    }

    [Fact]
    public void Crossings_inside_the_buffer_are_ignored()
    {
        Cross(distance: 40, buffer: 50).Should().Be(GeofenceTransition.None);
        Cross(distance: 40, buffer: 25).Should().Be(GeofenceTransition.Enter);
    }

    [Fact]
    public void Default_buffer_is_the_old_25_m_band()
    {
        var state = new GeofenceZoneState(false, T0);
        var a = GeofenceStateEvaluator.Evaluate(state, true, 20, T0.AddSeconds(15));
        var b = GeofenceStateEvaluator.Evaluate(a.State, true, 20, T0.AddSeconds(30));
        b.Transition.Should().Be(GeofenceTransition.None);
    }
}
