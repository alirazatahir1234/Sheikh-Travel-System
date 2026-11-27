using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GeofenceRuleEvaluatorTests
{
    private static readonly DateTime T0 = new(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);
    private static readonly GeofenceSchedule WorkHours = new(1 | 2 | 4 | 8 | 16 | 32, new TimeSpan(8, 0, 0), new TimeSpan(20, 0, 0));

    private static GeofenceVisitState Visit(DateTime entered, DateTime? idleSince = null, bool dwellBreached = false, bool idleBreached = false)
        => new(entered, entered, 0, idleSince, 0, DwellBreached: dwellBreached, IdleBreached: idleBreached);

    [Fact]
    public void Speed_breach_needs_a_limit_and_a_higher_speed()
    {
        var rules = GeofenceRules.None with { SpeedLimitKmh = 30 };

        GeofenceRuleEvaluator.IsSpeedBreach(rules, 31).Should().BeTrue();
        GeofenceRuleEvaluator.IsSpeedBreach(rules, 30).Should().BeFalse("at the limit is allowed");
        GeofenceRuleEvaluator.IsSpeedBreach(GeofenceRules.None, 200).Should().BeFalse("no limit set");
    }

    [Fact]
    public void Dwell_fires_once_after_the_limit()
    {
        var rules = GeofenceRules.None with { DwellMinutes = 30 };

        GeofenceRuleEvaluator.IsDwellBreach(rules, Visit(T0), T0.AddMinutes(29)).Should().BeFalse();
        GeofenceRuleEvaluator.IsDwellBreach(rules, Visit(T0), T0.AddMinutes(30)).Should().BeTrue();
        GeofenceRuleEvaluator.IsDwellBreach(rules, Visit(T0, dwellBreached: true), T0.AddHours(3)).Should().BeFalse(
            "a visit alerts once");
    }

    [Fact]
    public void Idle_fires_once_for_a_long_enough_streak()
    {
        var rules = GeofenceRules.None with { IdleMinutes = 10 };

        GeofenceRuleEvaluator.IsIdleBreach(rules, Visit(T0, idleSince: T0), T0.AddMinutes(9)).Should().BeFalse();
        GeofenceRuleEvaluator.IsIdleBreach(rules, Visit(T0, idleSince: T0), T0.AddMinutes(10)).Should().BeTrue();
        GeofenceRuleEvaluator.IsIdleBreach(rules, Visit(T0), T0.AddHours(1)).Should().BeFalse("not idle now");
        GeofenceRuleEvaluator.IsIdleBreach(rules, Visit(T0, idleSince: T0, idleBreached: true), T0.AddHours(1))
            .Should().BeFalse("a visit alerts once");
    }

    [Fact]
    public void Exits_outside_a_set_schedule_are_after_hours()
    {
        var rules = GeofenceRules.None with { Schedule = WorkHours };

        GeofenceRuleEvaluator.IsAfterHoursExit(rules, new DateTime(2026, 11, 27, 21, 30, 0)).Should().BeTrue("Friday 21:30");
        GeofenceRuleEvaluator.IsAfterHoursExit(rules, new DateTime(2026, 11, 27, 12, 0, 0)).Should().BeFalse();
        GeofenceRuleEvaluator.IsAfterHoursExit(GeofenceRules.None, new DateTime(2026, 11, 27, 3, 0, 0))
            .Should().BeFalse("no schedule means always active");
    }

    [Fact]
    public void Not_arrived_is_due_after_the_deadline_on_an_active_day_without_an_arrival()
    {
        var rules = GeofenceRules.None with { NotArrivedBy = new TimeSpan(9, 0, 0), Schedule = WorkHours };
        var friday0905 = new DateTime(2026, 11, 27, 9, 5, 0);

        GeofenceRuleEvaluator.IsNotArrivedDue(rules, friday0905, anyArrivalToday: false).Should().BeTrue();
        GeofenceRuleEvaluator.IsNotArrivedDue(rules, friday0905, anyArrivalToday: true).Should().BeFalse();
        GeofenceRuleEvaluator.IsNotArrivedDue(rules, new DateTime(2026, 11, 27, 8, 59, 0), false).Should().BeFalse();
        GeofenceRuleEvaluator.IsNotArrivedDue(rules, new DateTime(2026, 11, 29, 10, 0, 0), false)
            .Should().BeFalse("Sunday is not an active day");
    }

    [Fact]
    public void Cooldown_suppresses_repeats_inside_the_window()
    {
        GeofenceRuleEvaluator.IsInCooldown(T0, T0.AddMinutes(14), 15).Should().BeTrue();
        GeofenceRuleEvaluator.IsInCooldown(T0, T0.AddMinutes(15), 15).Should().BeFalse();
        GeofenceRuleEvaluator.IsInCooldown(null, T0, 15).Should().BeFalse();
    }

    [Fact]
    public void Rules_follow_the_schedule()
    {
        var rules = GeofenceRules.None with { Schedule = WorkHours };

        GeofenceRuleEvaluator.RulesActive(rules, new DateTime(2026, 11, 27, 10, 0, 0)).Should().BeTrue();
        GeofenceRuleEvaluator.RulesActive(rules, new DateTime(2026, 11, 29, 10, 0, 0)).Should().BeFalse();
        GeofenceRuleEvaluator.RulesActive(GeofenceRules.None, new DateTime(2026, 11, 29, 3, 0, 0)).Should().BeTrue();
    }

    [Fact]
    public void New_event_types_have_the_designed_severity()
    {
        GpsAlertSeverity.SeverityFor(GeofenceRuleEvents.Dwell).Should().Be("high");
        GpsAlertSeverity.SeverityFor(GeofenceRuleEvents.Overspeed).Should().Be("high");
        GpsAlertSeverity.SeverityFor(GeofenceRuleEvents.Idle).Should().Be("high");
        GpsAlertSeverity.SeverityFor(GeofenceRuleEvents.NotArrived).Should().Be("critical");
        GpsAlertSeverity.SeverityFor(GeofenceRuleEvents.AfterHoursExit).Should().Be("critical");
        GpsAlertSeverity.SeverityFor("geofence_exit").Should().Be("high", "existing levels are unchanged");
    }
}
