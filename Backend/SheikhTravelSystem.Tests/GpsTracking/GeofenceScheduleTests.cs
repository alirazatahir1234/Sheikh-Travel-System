using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GeofenceScheduleTests
{
    private const byte MonToSat = 1 | 2 | 4 | 8 | 16 | 32;

    // 2026-11-23 is a Monday.
    private static DateTime Local(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0);

    [Fact]
    public void Always_is_active_at_any_time()
    {
        GeofenceSchedule.Always.IsAlways.Should().BeTrue();
        GeofenceSchedule.Always.IsActiveAt(Local(29, 3)).Should().BeTrue();
    }

    [Fact]
    public void Day_window_is_active_inside_hours_on_listed_days_only()
    {
        var schedule = new GeofenceSchedule(MonToSat, new TimeSpan(8, 0, 0), new TimeSpan(20, 0, 0));

        schedule.IsActiveAt(Local(23, 8)).Should().BeTrue("Monday 08:00 is the first minute");
        schedule.IsActiveAt(Local(23, 19, 59)).Should().BeTrue();
        schedule.IsActiveAt(Local(23, 20)).Should().BeFalse("the end is exclusive");
        schedule.IsActiveAt(Local(23, 7, 59)).Should().BeFalse();
        schedule.IsActiveAt(Local(29, 12)).Should().BeFalse("Sunday is not listed");
    }

    [Fact]
    public void Overnight_window_belongs_to_the_day_it_started()
    {
        // Friday and Saturday nights, 20:00–06:00.
        var schedule = new GeofenceSchedule(16 | 32, new TimeSpan(20, 0, 0), new TimeSpan(6, 0, 0));

        schedule.IsActiveAt(Local(27, 22)).Should().BeTrue("Friday 22:00");
        schedule.IsActiveAt(Local(28, 3)).Should().BeTrue("Saturday 03:00 is still Friday night");
        schedule.IsActiveAt(Local(29, 3)).Should().BeTrue("Sunday 03:00 is still Saturday night");
        schedule.IsActiveAt(Local(30, 3)).Should().BeFalse("Monday 03:00 would be Sunday night, which is not listed");
        schedule.IsActiveAt(Local(27, 12)).Should().BeFalse("midday is outside the window");
    }

    [Fact]
    public void Missing_or_equal_hours_mean_the_whole_listed_day()
    {
        new GeofenceSchedule(1, null, null).IsActiveAt(Local(23, 2)).Should().BeTrue();
        new GeofenceSchedule(1, new TimeSpan(9, 0, 0), new TimeSpan(9, 0, 0)).IsActiveAt(Local(23, 23)).Should().BeTrue();
        new GeofenceSchedule(1, null, null).IsActiveAt(Local(24, 2)).Should().BeFalse("Tuesday is not listed");
    }

    [Fact]
    public void Day_bits_follow_monday_first()
    {
        GeofenceSchedule.DayBit(DayOfWeek.Monday).Should().Be(1);
        GeofenceSchedule.DayBit(DayOfWeek.Saturday).Should().Be(32);
        GeofenceSchedule.DayBit(DayOfWeek.Sunday).Should().Be(64);
    }
}
