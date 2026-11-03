using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GeofenceStateEvaluatorTests
{
    private static readonly DateTime T0 = new(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);
    private const double Far = 500;

    /// <summary>Runs samples through the state machine and returns every committed transition.</summary>
    private static (GeofenceZoneState? State, List<GeofenceTransition> Transitions) Run(
        GeofenceZoneState? state, params (bool Inside, double Distance)[] samples)
    {
        var transitions = new List<GeofenceTransition>();
        var t = T0;
        foreach (var (inside, distance) in samples)
        {
            t = t.AddSeconds(15);
            var r = GeofenceStateEvaluator.Evaluate(state, inside, distance, t);
            state = r.State;
            if (r.Transition != GeofenceTransition.None)
                transitions.Add(r.Transition);
        }

        return (state, transitions);
    }

    [Fact]
    public void FirstObservation_SeedsStateWithoutAlert()
    {
        var r = GeofenceStateEvaluator.Evaluate(null, true, Far, T0);
        r.Transition.Should().Be(GeofenceTransition.None);
        r.StateChanged.Should().BeTrue();
        r.State.IsInside.Should().BeTrue();
    }

    [Fact]
    public void Crossing_RequiresConfirmingSample_ThenCommitsOnce()
    {
        var (state, transitions) = Run(new GeofenceZoneState(false, T0),
            (true, Far), (true, Far), (true, Far), (true, Far));

        transitions.Should().Equal(GeofenceTransition.Enter);
        state!.IsInside.Should().BeTrue();
        state.SinceUtc.Should().Be(T0.AddSeconds(15), "since = first sample on the new side");
    }

    [Fact]
    public void ExitOnlyRule_StillDetectsExitAfterUnalertedEnter()
    {
        // Old behaviour derived wasInside from enter alerts — with only Exit enabled, exit never fired.
        var (_, transitions) = Run(new GeofenceZoneState(false, T0),
            (true, Far), (true, Far), (false, Far), (false, Far));

        transitions.Should().Equal(GeofenceTransition.Enter, GeofenceTransition.Exit);
        transitions.Count(t => GeofenceStateEvaluator.ShouldAlert(t, alertOnEnter: false, alertOnExit: true))
            .Should().Be(1);
    }

    [Fact]
    public void EnterOnlyRule_FiresOnEveryReEntry()
    {
        // Old behaviour: with only Enter on, last alert stayed "enter" forever so re-entry never fired.
        var (_, transitions) = Run(new GeofenceZoneState(false, T0),
            (true, Far), (true, Far), (false, Far), (false, Far), (true, Far), (true, Far));

        transitions.Count(t => GeofenceStateEvaluator.ShouldAlert(t, alertOnEnter: true, alertOnExit: false))
            .Should().Be(2);
    }

    [Fact]
    public void SingleSampleFlap_IsIgnored()
    {
        var (state, transitions) = Run(new GeofenceZoneState(true, T0),
            (false, Far), (true, Far), (false, Far), (true, Far));

        transitions.Should().BeEmpty();
        state!.IsInside.Should().BeTrue();
        state.PendingInside.Should().BeNull();
    }

    [Fact]
    public void JitterWithinHysteresisBand_NeverFlipsState()
    {
        var nearEdge = GeofenceStateEvaluator.HysteresisMeters - 5;
        var (state, transitions) = Run(new GeofenceZoneState(true, T0),
            (false, nearEdge), (false, nearEdge), (false, nearEdge), (true, nearEdge), (false, nearEdge));

        transitions.Should().BeEmpty();
        state!.IsInside.Should().BeTrue();
    }

    [Fact]
    public void ClearExitBeyondBand_Commits()
    {
        var beyond = GeofenceStateEvaluator.HysteresisMeters + 10;
        var (_, transitions) = Run(new GeofenceZoneState(true, T0), (false, beyond), (false, beyond));
        transitions.Should().Equal(GeofenceTransition.Exit);
    }

    [Fact]
    public void NoChange_ReportsStateUnchanged()
    {
        var r = GeofenceStateEvaluator.Evaluate(new GeofenceZoneState(true, T0), true, Far, T0.AddMinutes(1));
        r.StateChanged.Should().BeFalse();
        r.Transition.Should().Be(GeofenceTransition.None);
    }

    [Theory]
    [InlineData(GeofenceTransition.Enter, true, false, true)]
    [InlineData(GeofenceTransition.Enter, false, true, false)]
    [InlineData(GeofenceTransition.Exit, false, true, true)]
    [InlineData(GeofenceTransition.Exit, true, false, false)]
    [InlineData(GeofenceTransition.None, true, true, false)]
    public void ShouldAlert_OnlyForConfiguredDirection(GeofenceTransition t, bool onEnter, bool onExit, bool expected)
        => GeofenceStateEvaluator.ShouldAlert(t, onEnter, onExit).Should().Be(expected);

    [Fact]
    public void DedupWindow_SuppressesRepeatAlertForSameDirection()
    {
        GeofenceStateEvaluator.IsWithinDedupWindow(T0, T0.AddSeconds(30)).Should().BeTrue();
        GeofenceStateEvaluator.IsWithinDedupWindow(T0, T0.AddMinutes(GeofenceStateEvaluator.AlertDedupMinutes + 1))
            .Should().BeFalse();
        GeofenceStateEvaluator.IsWithinDedupWindow(null, T0).Should().BeFalse();
    }

    [Theory]
    [InlineData("geofenceEnter")]
    [InlineData("geofenceExit")]
    public void TraccarGeofenceEvents_AreNotImported_SoOneCrossingIsOneAlert(string traccarType)
        => TraccarAlertEventMapper.MapEventType(traccarType).Should().BeNull();
}
