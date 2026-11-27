using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GeofenceVisitTrackerTests
{
    private static readonly DateTime T0 = new(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Opening_a_visit_records_entry_speed_and_idle_start()
    {
        var idleOpen = GeofenceVisitTracker.Open(T0, T0, 0, ignition: true);
        idleOpen.IdleSinceUtc.Should().Be(T0);
        idleOpen.IdleSeconds.Should().Be(0);

        var movingOpen = GeofenceVisitTracker.Open(T0, T0, 24, ignition: true);
        movingOpen.IdleSinceUtc.Should().BeNull();
        movingOpen.MaxSpeedKmh.Should().Be(24);
    }

    [Fact]
    public void Idle_time_accrues_while_the_engine_runs_without_moving()
    {
        var visit = GeofenceVisitTracker.Open(T0, T0, 0, ignition: true);
        visit = GeofenceVisitTracker.Observe(visit, T0.AddMinutes(2), 0, ignition: true);
        visit = GeofenceVisitTracker.Observe(visit, T0.AddMinutes(4), 1, ignition: true);

        visit.IdleSeconds.Should().Be(240);
        visit.IdleSinceUtc.Should().Be(T0, "the idle streak started at the first idle fix");
    }

    [Fact]
    public void Moving_or_engine_off_is_not_idle()
    {
        var visit = GeofenceVisitTracker.Open(T0, T0, 0, ignition: false);
        visit = GeofenceVisitTracker.Observe(visit, T0.AddMinutes(5), 0, ignition: false);
        visit.IdleSeconds.Should().Be(0);
        visit.IdleSinceUtc.Should().BeNull();

        var unknown = GeofenceVisitTracker.Open(T0, T0, 0, ignition: null);
        unknown.IdleSinceUtc.Should().BeNull("unknown ignition is not idle");
    }

    [Fact]
    public void Gaps_longer_than_ten_minutes_are_not_counted_and_restart_the_streak()
    {
        var visit = GeofenceVisitTracker.Open(T0, T0, 0, ignition: true);
        visit = GeofenceVisitTracker.Observe(visit, T0.AddMinutes(30), 0, ignition: true);

        visit.IdleSeconds.Should().Be(0);
        visit.IdleSinceUtc.Should().Be(T0.AddMinutes(30));
    }

    [Fact]
    public void Leaving_idle_closes_the_streak_but_keeps_the_time()
    {
        var visit = GeofenceVisitTracker.Open(T0, T0, 0, ignition: true);
        visit = GeofenceVisitTracker.Observe(visit, T0.AddMinutes(3), 35, ignition: true);

        visit.IdleSeconds.Should().Be(180);
        visit.IdleSinceUtc.Should().BeNull();
        visit.MaxSpeedKmh.Should().Be(35);
    }

    [Fact]
    public void Replayed_or_out_of_order_fixes_change_nothing()
    {
        var visit = GeofenceVisitTracker.Open(T0, T0.AddMinutes(5), 0, ignition: true);
        var again = GeofenceVisitTracker.Observe(visit, T0.AddMinutes(5), 90, ignition: true);
        var older = GeofenceVisitTracker.Observe(visit, T0.AddMinutes(1), 90, ignition: true);

        again.Should().Be(visit);
        older.Should().Be(visit);
    }
}
