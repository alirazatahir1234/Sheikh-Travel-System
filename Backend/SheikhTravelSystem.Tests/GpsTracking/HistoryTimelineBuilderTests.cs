using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// The History workspace shows one calculation three ways (strip, day rows, trip rows). These tests
/// pin the cut itself and, in every scenario, that the three views add up.
/// </summary>
public class HistoryTimelineBuilderTests
{
    private static readonly TimeZoneInfo Karachi = TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi");

    /// <summary>Fri 20 Nov 2026, 00:00 in Karachi (UTC+5).</summary>
    private static readonly DateTime T0 = new(2026, 11, 19, 19, 0, 0, DateTimeKind.Utc);

    /// <summary>One fix of <see cref="Drive"/> further north: about 0.9 km.</summary>
    private const double Step = 0.0081;

    private const double Lng = 74.30;

    private static TripReplayPositionDto P(
        DateTime t,
        double lat,
        decimal speed,
        decimal? odometer = null,
        double lng = Lng) =>
        new(t, lat, lng, speed, null, false, null, null, null, null, odometer);

    /// <summary>Fixes one minute apart heading north at 54 km/h: <paramref name="minutes"/> + 1 fixes.</summary>
    private static List<TripReplayPositionDto> Drive(DateTime start, int minutes, double fromLat)
    {
        var fixes = new List<TripReplayPositionDto>();
        for (var i = 0; i <= minutes; i++)
            fixes.Add(P(start.AddMinutes(i), fromLat + i * Step, 54m));
        return fixes;
    }

    /// <summary>Standing fixes every <paramref name="every"/> minutes from start to start + minutes.</summary>
    private static List<TripReplayPositionDto> Stand(DateTime start, int minutes, double lat, int every = 1)
    {
        var fixes = new List<TripReplayPositionDto>();
        for (var m = 0; m < minutes; m += every)
            fixes.Add(P(start.AddMinutes(m), lat, 0m));
        fixes.Add(P(start.AddMinutes(minutes), lat, 0m));
        return fixes;
    }

    private static List<HistoryTimelineSegmentDto> Trips(HistoryTimelineDto t) =>
        t.Segments.Where(s => s.Kind == HistorySegmentKinds.Trip).ToList();

    private static List<HistoryTimelineSegmentDto> Standstills(HistoryTimelineDto t) =>
        t.Segments.Where(s => s.Kind != HistorySegmentKinds.Trip).ToList();

    /// <summary>The three views of one calculation agree. Asserted in every scenario below.</summary>
    private static void AssertAddsUp(HistoryTimelineDto t)
    {
        var trips = Trips(t);
        var standstills = Standstills(t);

        t.Totals.DistanceKm.Should().Be(t.Days.Sum(d => d.DistanceKm), "days add up to the strip's distance");
        t.Totals.DistanceKm.Should().Be(trips.Sum(s => s.DistanceKm ?? 0m), "trips add up to the strip's distance");
        t.Totals.DrivingMinutes.Should().Be(t.Days.Sum(d => d.DrivingMinutes), "days add up to the strip's driving time");
        t.Totals.DrivingMinutes.Should().Be(trips.Sum(s => s.DurationMinutes), "trips add up to the strip's driving time");
        (t.Totals.DrivingMinutes + t.Totals.StoppedMinutes).Should().Be(t.Totals.WindowMinutes, "driving + stopped is the period");
        t.Totals.TripCount.Should().Be(trips.Count);
        t.Totals.StopCount.Should().Be(standstills.Count, "Stops counts every standstill in the list");
        t.Totals.ParkingCount.Should().Be(standstills.Count(s => s.Kind == HistorySegmentKinds.Parking));
        t.Totals.ParkingCount.Should().BeLessThanOrEqualTo(t.Totals.StopCount);

        foreach (var day in t.Days)
        {
            var dayTrips = trips.Where(s => s.Date == day.Date).ToList();
            day.DistanceKm.Should().Be(dayTrips.Sum(s => s.DistanceKm ?? 0m), $"trips of {day.Date} add up to its row");
            day.DrivingMinutes.Should().Be(dayTrips.Sum(s => s.DurationMinutes), $"trips of {day.Date} add up to its row");
            day.TripCount.Should().Be(dayTrips.Count);
            day.StopCount.Should().Be(standstills.Count(s => s.Date == day.Date));
            day.ParkingCount.Should().Be(standstills.Count(s => s.Date == day.Date && s.Kind == HistorySegmentKinds.Parking));
        }

        t.Segments.Select(s => s.Date).Should().BeSubsetOf(t.Days.Select(d => d.Date));
        for (var i = 1; i < t.Segments.Count; i++)
            t.Segments[i].StartTime.Should().BeOnOrAfter(t.Segments[i - 1].EndTime, "segments are in order and do not overlap");
    }

    // ---- adding up ------------------------------------------------------------------------

    [Fact]
    public void ThreeDays_DaysAddUpToTheStrip_AndTripsAddUpToTheirDay()
    {
        // Each local day: parked overnight, a morning trip, a 30-minute stop, a trip back, parked.
        var fixes = new List<TripReplayPositionDto>();
        const double home = 31.50;
        for (var d = 0; d < 3; d++)
        {
            var day = T0.AddDays(d);
            fixes.AddRange(Stand(day, 8 * 60, home, every: 15));                    // 00:00–08:00
            var out1 = Drive(day.AddHours(8).AddMinutes(1), 20 + d, home);          // leaves 08:01
            fixes.AddRange(out1);
            var away = out1[^1].Latitude;
            fixes.AddRange(Stand(out1[^1].Timestamp.AddMinutes(1), 30, away));      // stopped 30 min
            fixes.AddRange(Drive(day.AddHours(9).AddMinutes(30), 15, away));        // on again
            var far = fixes[^1].Latitude;
            fixes.AddRange(Stand(day.AddHours(10), 14 * 60 - 15, far, every: 15));  // parked to 23:45
            // Back home for the next day in one silent hop inside the last 15 minutes is not needed:
            // the next day's first standing fix is far from here, which is a silence with displacement.
        }

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddDays(3), Karachi);

        AssertAddsUp(t);
        t.Days.Should().HaveCount(3);
        t.Days.Select(d => d.Date).Should().Equal("2026-11-20", "2026-11-21", "2026-11-22");
        t.Days.Should().OnlyContain(d => d.TripCount >= 2 && d.DistanceKm > 0m);
        t.Totals.WindowMinutes.Should().Be(3 * 24 * 60);
        t.Totals.AvgSpeedKmh.Should().BeLessThanOrEqualTo(t.Totals.MaxSpeedKmh);
        t.MinStopMinutes.Should().Be(5);
        t.ParkingMinutes.Should().Be(120);
    }

    // ---- trips and standstills --------------------------------------------------------------

    [Fact]
    public void StandstillOfFiveMinutes_EndsTheTrip()
    {
        var first = Drive(T0, 10, 31.50);
        var at = first[^1].Latitude;
        var fixes = first
            .Concat(Stand(T0.AddMinutes(11), 5, at))
            .Concat(Drive(T0.AddMinutes(17), 10, at + Step))
            .ToList();

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(27), Karachi);

        AssertAddsUp(t);
        t.Segments.Select(s => s.Kind).Should().Equal(
            HistorySegmentKinds.Trip, HistorySegmentKinds.Stop, HistorySegmentKinds.Trip);
        t.Segments[1].StartTime.Should().Be(T0.AddMinutes(11));
        t.Segments[1].EndTime.Should().Be(T0.AddMinutes(16));
        t.Segments[1].DurationMinutes.Should().Be(5);
        t.Segments[0].EndTime.Should().Be(t.Segments[1].StartTime, "the trip runs up to the standstill");
        t.Segments[2].StartTime.Should().Be(t.Segments[1].EndTime, "the next trip starts where the standstill ends");
        t.Segments[0].DistanceKm.Should().Be(9.0m);
        t.Segments[0].MaxSpeedKmh.Should().Be(54m);
    }

    [Fact]
    public void StandstillUnderFiveMinutes_StaysInsideTheTrip_AsABriefStop()
    {
        var first = Drive(T0, 10, 31.50);
        var at = first[^1].Latitude;
        var fixes = first
            .Concat(Stand(T0.AddMinutes(11), 4, at))
            .Concat(Drive(T0.AddMinutes(16), 10, at + Step))
            .ToList();

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(26), Karachi);

        AssertAddsUp(t);
        t.Segments.Should().ContainSingle().Which.Kind.Should().Be(HistorySegmentKinds.Trip);
        var trip = t.Segments[0];
        trip.StartTime.Should().Be(T0);
        trip.EndTime.Should().Be(T0.AddMinutes(26));
        trip.DurationMinutes.Should().Be(26);
        trip.BriefStops.Should().ContainSingle().Which.DurationSeconds.Should().Be(240);
        t.Totals.StopCount.Should().Be(0, "a brief stop is not a stop in the strip");
    }

    [Fact]
    public void StandstillOfTwoHours_IsParking_AMinuteLessIsAStop()
    {
        var parked = Stand(T0, 120, 31.50, every: 10);
        var stopped = Stand(T0, 110, 31.50, every: 10);
        stopped.Add(P(T0.AddMinutes(119), 31.50, 0m));

        var parking = HistoryTimelineBuilder.Build(parked, T0, T0.AddMinutes(120), Karachi);
        var stop = HistoryTimelineBuilder.Build(stopped, T0, T0.AddMinutes(119), Karachi);

        AssertAddsUp(parking);
        AssertAddsUp(stop);
        parking.Segments.Should().ContainSingle().Which.Kind.Should().Be(HistorySegmentKinds.Parking);
        stop.Segments.Should().ContainSingle().Which.Kind.Should().Be(HistorySegmentKinds.Stop);
        parking.Totals.StopCount.Should().Be(1, "Stops counts parking too");
        parking.Totals.ParkingCount.Should().Be(1);
        stop.Totals.ParkingCount.Should().Be(0);
    }

    [Fact]
    public void JitterWhileParked_IsNotATrip()
    {
        // Three hours in one spot; one fix lands 60 m away and the next is back.
        var fixes = Stand(T0, 180, 31.50, every: 10);
        fixes[6] = fixes[6] with { Latitude = 31.50 + 0.00054 };

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(180), Karachi);

        AssertAddsUp(t);
        t.Segments.Should().ContainSingle().Which.Kind.Should().Be(HistorySegmentKinds.Parking);
        t.Segments[0].DurationMinutes.Should().Be(180);
        t.Totals.TripCount.Should().Be(0);
        t.Totals.DistanceKm.Should().Be(0m);
        t.Totals.DrivingMinutes.Should().Be(0);
    }

    // ---- silences ---------------------------------------------------------------------------

    [Fact]
    public void SilentParking_ThenAFixNearby_IsOneStandstill()
    {
        // The tracker sleeps for 13 hours and wakes 100 m from where it stopped.
        var first = Drive(T0, 10, 31.50);
        var at = first[^1].Latitude;
        var wake = T0.AddMinutes(10).AddHours(13);
        var fixes = first.Concat(Drive(wake, 10, at + 0.0009)).ToList();

        var t = HistoryTimelineBuilder.Build(fixes, T0, wake.AddMinutes(10), Karachi);

        AssertAddsUp(t);
        t.Segments.Select(s => s.Kind).Should().Equal(
            HistorySegmentKinds.Trip, HistorySegmentKinds.Parking, HistorySegmentKinds.Trip);
        t.Segments[1].StartTime.Should().Be(T0.AddMinutes(10));
        t.Segments[1].EndTime.Should().Be(wake);
        t.Segments[1].DurationMinutes.Should().Be(13 * 60);
        t.Totals.DrivingMinutes.Should().Be(20, "the silence is not driving");
        t.Totals.NoSignalMinutes.Should().Be(0);
    }

    [Fact]
    public void SilenceWithDisplacement_IsStandingThenAnEstimatedDrive()
    {
        // Parked, silent for ten hours, next seen 3 km away at 36 km/h: five minutes of driving.
        var seen = T0.AddMinutes(610);
        var fixes = new List<TripReplayPositionDto>
        {
            P(T0, 31.50, 0m),
            P(T0.AddMinutes(10), 31.50, 0m),
            P(seen, 31.527, 36m)
        };
        fixes.AddRange(Drive(seen.AddMinutes(1), 9, 31.527 + Step));

        var t = HistoryTimelineBuilder.Build(fixes, T0, seen.AddMinutes(10), Karachi);

        AssertAddsUp(t);
        t.Segments.Select(s => s.Kind).Should().Equal(HistorySegmentKinds.Parking, HistorySegmentKinds.Trip);
        var parking = t.Segments[0];
        var trip = t.Segments[1];
        parking.DurationMinutes.Should().Be(605);
        parking.StartLatitude.Should().Be(31.50, "it stood where it was last seen standing");
        trip.StartTime.Should().Be(parking.EndTime);
        trip.StartTime.Should().BeCloseTo(seen.AddMinutes(-5), TimeSpan.FromSeconds(30));
        trip.NoSignal.Should().ContainSingle().Which.DistanceKm.Should().Be(3.0m);
        trip.DistanceKm.Should().Be(12.0m, "3 km without signal + 10 fixes of 0.9 km");
        t.Totals.NoSignalMinutes.Should().Be(5);
        t.Totals.DrivingMinutes.Should().Be(15, "only the estimated drive counts, not the ten hours");
    }

    [Fact]
    public void SilenceEnteredWhileDriving_ArrivesFirst_ThenStandsAtTheFarEnd()
    {
        // Last fix at 54 km/h, then nothing for eight hours, then standing 6 km further on.
        var first = Drive(T0, 5, 31.50);
        var last = first[^1];
        var farLat = last.Latitude + 0.054;
        var fixes = first.ToList();
        fixes.Add(P(T0.AddMinutes(485), farLat, 0m));
        fixes.Add(P(T0.AddMinutes(495), farLat, 0m));

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(495), Karachi);

        AssertAddsUp(t);
        t.Segments.Select(s => s.Kind).Should().Equal(HistorySegmentKinds.Trip, HistorySegmentKinds.Parking);
        var trip = t.Segments[0];
        var parking = t.Segments[1];
        trip.NoSignal.Should().ContainSingle();
        trip.EndTime.Should().BeCloseTo(T0.AddMinutes(5 + 6.67), TimeSpan.FromSeconds(30));
        trip.EndLatitude.Should().Be(farLat);
        parking.StartTime.Should().Be(trip.EndTime);
        parking.StartLatitude.Should().Be(farLat, "it stands where it is next seen standing");
        parking.EndTime.Should().Be(T0.AddMinutes(495));
    }

    [Fact]
    public void ShortSilenceThatOnlyADriveExplains_IsAllDriving_AndFlagged()
    {
        // 26 km in a 30-minute silence at 50 km/h: there was no time to stand.
        var a = P(T0.AddMinutes(5), 31.50, 50m);
        var b = P(T0.AddMinutes(35), 31.50 + 0.2338, 50m);
        var fixes = new List<TripReplayPositionDto> { P(T0, 31.50 - 0.03, 50m), a, b, P(T0.AddMinutes(40), b.Latitude + 0.03, 50m) };

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(40), Karachi);

        AssertAddsUp(t);
        t.Segments.Should().ContainSingle().Which.Kind.Should().Be(HistorySegmentKinds.Trip);
        var trip = t.Segments[0];
        trip.DurationMinutes.Should().Be(40);
        trip.NoSignal.Should().ContainSingle();
        trip.NoSignal![0].StartTime.Should().Be(a.Timestamp);
        trip.NoSignal[0].EndTime.Should().Be(b.Timestamp);
        trip.BriefStops.Should().BeNull();
        t.Totals.NoSignalMinutes.Should().Be(30);
    }

    // ---- window edges -----------------------------------------------------------------------

    [Fact]
    public void StandingAtBothEdges_FillsTheWindow()
    {
        // First seen two hours in, last seen three hours before the end, standing both times.
        var fixes = new List<TripReplayPositionDto>
        {
            P(T0.AddMinutes(120), 31.50, 0m),
            P(T0.AddMinutes(420), 31.50, 0m)
        };

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(600), Karachi);

        AssertAddsUp(t);
        var only = t.Segments.Should().ContainSingle().Subject;
        only.Kind.Should().Be(HistorySegmentKinds.Parking);
        only.StartTime.Should().Be(T0);
        only.EndTime.Should().Be(T0.AddMinutes(600));
        t.Totals.StoppedMinutes.Should().Be(600);
    }

    [Fact]
    public void EmptyRoute_IsAllStopped_WithOneRowPerLocalDay()
    {
        var t = HistoryTimelineBuilder.Build([], T0, T0.AddDays(2), Karachi);

        AssertAddsUp(t);
        t.Segments.Should().BeEmpty();
        t.Days.Select(d => d.Date).Should().Equal("2026-11-20", "2026-11-21");
        t.Totals.WindowMinutes.Should().Be(2880);
        t.Totals.StoppedMinutes.Should().Be(2880);
        t.Totals.AvgSpeedKmh.Should().Be(0m);
    }

    [Fact]
    public void SingleStandingFix_IsOneStandstillOverTheWindow()
    {
        var t = HistoryTimelineBuilder.Build([P(T0.AddHours(5), 31.50, 0m)], T0, T0.AddHours(10), Karachi);

        AssertAddsUp(t);
        var only = t.Segments.Should().ContainSingle().Subject;
        only.Kind.Should().Be(HistorySegmentKinds.Parking);
        only.DurationMinutes.Should().Be(600);
    }

    [Fact]
    public void WindowThatEndsBeforeItStarts_IsEmpty()
    {
        var t = HistoryTimelineBuilder.Build(Drive(T0, 10, 31.50), T0, T0.AddMinutes(-1), Karachi);

        t.Segments.Should().BeEmpty();
        t.Days.Should().BeEmpty();
        t.Totals.WindowMinutes.Should().Be(0);
    }

    // ---- ongoing ----------------------------------------------------------------------------

    /// <summary>
    /// Parked overnight, a trip from 12:00 to 13:27 local, then standing: fixes until 13:57 and none
    /// after, as a parked tracker goes quiet. The window ends at 14:19, 52 minutes into the stop.
    /// </summary>
    private static (List<TripReplayPositionDto> Fixes, DateTime StoppedAt, DateTime End) DriveThenStandUntilTheEnd()
    {
        var drive = Drive(T0.AddHours(12), 86, 31.50);
        var stoppedAt = T0.AddHours(12).AddMinutes(87);
        var fixes = drive.Concat(Stand(stoppedAt, 30, drive[^1].Latitude)).ToList();
        return (fixes, stoppedAt, stoppedAt.AddMinutes(52));
    }

    [Fact]
    public void StandstillStillUnderWay_InAWindowThatEndsNow_IsOngoing()
    {
        var (fixes, stoppedAt, end) = DriveThenStandUntilTheEnd();

        var t = HistoryTimelineBuilder.Build(fixes, T0, end, Karachi, nowUtc: end.AddSeconds(30));

        AssertAddsUp(t);
        var last = t.Segments[^1];
        last.Kind.Should().Be(HistorySegmentKinds.Stop);
        last.Ongoing.Should().BeTrue();
        last.StartTime.Should().Be(stoppedAt);
        last.EndTime.Should().Be(end, "an unfinished standstill runs to the end of the window");
        last.DurationMinutes.Should().Be(52, "the time so far");

        var withoutClock = HistoryTimelineBuilder.Build(fixes, T0, end, Karachi);
        t.Totals.Should().Be(withoutClock.Totals, "marking it ongoing changes no figure");
        t.Days.Should().Equal(withoutClock.Days);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(60, false)]
    public void WindowEndingShortlyBeforeTheRequest_StillEndsNow(int minutesBeforeTheRequest, bool ongoing)
    {
        var (fixes, _, end) = DriveThenStandUntilTheEnd();

        var t = HistoryTimelineBuilder.Build(fixes, T0, end, Karachi, nowUtc: end.AddMinutes(minutesBeforeTheRequest));

        t.Segments[^1].Ongoing.Should().Be(ongoing);
    }

    [Fact]
    public void OnlyTheLastStandstill_IsOngoing()
    {
        var (fixes, _, end) = DriveThenStandUntilTheEnd();

        var t = HistoryTimelineBuilder.Build(fixes, T0, end, Karachi, nowUtc: end);

        var standstills = Standstills(t);
        standstills.Should().HaveCount(2);
        standstills[0].Ongoing.Should().BeFalse("the night's parking ended when the trip began");
        t.Segments.Count(s => s.Ongoing).Should().Be(1);
    }

    [Fact]
    public void VehicleDrivingWhenTheWindowEnds_HasNoOngoingSegment()
    {
        var fixes = Drive(T0.AddHours(12), 30, 31.50);
        var end = fixes[^1].Timestamp;

        var t = HistoryTimelineBuilder.Build(fixes, T0, end, Karachi, nowUtc: end);

        AssertAddsUp(t);
        t.Segments[^1].Kind.Should().Be(HistorySegmentKinds.Trip);
        t.Segments.Should().OnlyContain(s => !s.Ongoing, "a trip in progress is not marked");
    }

    [Fact]
    public void WithoutTheTimeOfTheRequest_NothingIsOngoing()
    {
        var (fixes, _, end) = DriveThenStandUntilTheEnd();

        var t = HistoryTimelineBuilder.Build(fixes, T0, end, Karachi);

        t.Segments.Should().OnlyContain(s => !s.Ongoing);
    }

    // ---- local days -------------------------------------------------------------------------

    [Fact]
    public void TripOverMidnight_CountsInTheLocalDayItStarted()
    {
        // 23:00 Fri to 01:00 Sat in Karachi. Local midnight is 19:00 UTC, not UTC midnight.
        var from = new DateTime(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);
        var midnight = from.AddHours(1);
        var fixes = Stand(from, 30, 31.50, every: 10);                    // 23:00–23:30
        var drive = Drive(from.AddMinutes(40), 40, 31.50);                // 23:40–00:20
        fixes.AddRange(drive);
        fixes.AddRange(Stand(from.AddMinutes(90), 30, drive[^1].Latitude, every: 10));  // 00:30–01:00

        var t = HistoryTimelineBuilder.Build(fixes, from, from.AddHours(2), Karachi);

        AssertAddsUp(t);
        t.Days.Select(d => d.Date).Should().Equal("2026-11-20", "2026-11-21");
        t.Days[0].EndTime.Should().Be(midnight);
        t.Days[1].StartTime.Should().Be(midnight);

        var trip = Trips(t).Should().ContainSingle().Subject;
        trip.StartTime.Should().BeBefore(midnight);
        trip.EndTime.Should().BeAfter(midnight);
        trip.Date.Should().Be("2026-11-20");

        t.Days[0].TripCount.Should().Be(1);
        t.Days[0].DistanceKm.Should().Be(trip.DistanceKm);
        t.Days[1].TripCount.Should().Be(0);
        t.Days[1].DistanceKm.Should().Be(0m);
        t.Days[1].StopCount.Should().Be(1, "the standstill after midnight belongs to Saturday");
    }

    // ---- distance ---------------------------------------------------------------------------

    [Fact]
    public void Distance_UsesTheOdometer_WhenItIsPlausible()
    {
        // 0.9 km apart in a straight line, 1.4 km by the odometer: a winding road.
        var fixes = new List<TripReplayPositionDto>
        {
            P(T0, 31.50, 54m, odometer: 100.0m),
            P(T0.AddMinutes(1), 31.50 + Step, 54m, odometer: 101.4m),
            P(T0.AddMinutes(2), 31.50 + 2 * Step, 54m, odometer: 102.8m)
        };

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(2), Karachi);

        AssertAddsUp(t);
        t.Totals.DistanceKm.Should().Be(2.8m);
    }

    [Fact]
    public void Distance_FallsBackToTheStraightLine_WhenTheOdometerJumps()
    {
        var fixes = new List<TripReplayPositionDto>
        {
            P(T0, 31.50, 54m, odometer: 100.0m),
            P(T0.AddMinutes(1), 31.50 + Step, 54m, odometer: 0m),          // reset
            P(T0.AddMinutes(2), 31.50 + 2 * Step, 54m, odometer: 500m)     // jump
        };

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(2), Karachi);

        AssertAddsUp(t);
        t.Totals.DistanceKm.Should().Be(1.8m);
    }

    [Fact]
    public void FixThatNoVehicleCouldReach_IsIgnored()
    {
        var fixes = Drive(T0, 10, 31.50);
        fixes[5] = fixes[5] with { Latitude = 34.2, Longitude = 76.9 };   // ~400 km away for one fix

        var t = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(10), Karachi);

        AssertAddsUp(t);
        t.Segments.Should().ContainSingle().Which.Kind.Should().Be(HistorySegmentKinds.Trip);
        t.Totals.DistanceKm.Should().Be(9.0m);
    }

    // ---- places -----------------------------------------------------------------------------

    private sealed class FakeGeocoder : IReverseGeocodingService
    {
        public int Calls { get; private set; }

        /// <summary>Answer as if every place were already in the address cache.</summary>
        public bool Cached { get; init; }

        public Task<ReverseGeocodeResult?> GetAddressAsync(
            double latitude,
            double longitude,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default,
            bool allowGoogle = false)
        {
            Calls++;
            allowGoogle.Should().BeFalse("the timeline never pays for a lookup");
            var name = $"{Math.Round(latitude * 1000)} Canal Road";
            return Task.FromResult<ReverseGeocodeResult?>(
                new ReverseGeocodeResult(name, FromCache: Cached, PrimaryAddress: name));
        }
    }

    private static List<TripReplayPositionDto> StandDriveStandDriveStand(out double a, out double b, out double c)
    {
        a = 31.50;
        var fixes = Stand(T0, 10, a);
        var first = Drive(T0.AddMinutes(11), 10, a);
        fixes.AddRange(first);
        b = first[^1].Latitude;
        fixes.AddRange(Stand(T0.AddMinutes(22), 6, b));
        var second = Drive(T0.AddMinutes(29), 10, b);
        fixes.AddRange(second);
        c = second[^1].Latitude;
        fixes.AddRange(Stand(T0.AddMinutes(40), 10, c));
        return fixes;
    }

    [Fact]
    public async Task Places_AreLookedUpOncePerStandstill_AndATripTakesItsNeighboursNames()
    {
        var fixes = StandDriveStandDriveStand(out _, out _, out _);
        var geocoder = new FakeGeocoder();

        var built = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(50), Karachi);
        var t = await HistoryTimelineBuilder.WithAddressesAsync(built, geocoder, CancellationToken.None);

        AssertAddsUp(t);
        t.Segments.Select(s => s.Kind).Should().Equal(
            HistorySegmentKinds.Stop, HistorySegmentKinds.Trip, HistorySegmentKinds.Stop,
            HistorySegmentKinds.Trip, HistorySegmentKinds.Stop);
        geocoder.Calls.Should().Be(3, "three places; the trips reuse them");

        var names = Standstills(t).Select(s => s.StartAddress).ToList();
        names.Should().OnlyContain(n => n != null && n.EndsWith("Canal Road"));
        names.Should().OnlyHaveUniqueItems();
        t.Segments[1].StartAddress.Should().Be(names[0]);
        t.Segments[1].EndAddress.Should().Be(names[1]);
        t.Segments[3].StartAddress.Should().Be(names[1]);
        t.Segments[3].EndAddress.Should().Be(names[2]);
    }

    [Fact]
    public async Task Places_WithinTheStopRadius_ShareOneLookup()
    {
        // Parked at the depot, a trip out and back, parked again 60 m from the first spot.
        var fixes = Stand(T0, 130, 31.50, every: 10);
        var outbound = Drive(T0.AddMinutes(131), 10, 31.50);
        fixes.AddRange(outbound);
        var away = outbound[^1].Latitude;
        fixes.AddRange(Stand(T0.AddMinutes(142), 20, away));
        for (var i = 0; i <= 10; i++)
            fixes.Add(P(T0.AddMinutes(163 + i), away - i * Step + (i == 10 ? 0.00054 : 0), 54m));
        fixes.AddRange(Stand(T0.AddMinutes(174), 130, 31.50054, every: 10));
        var geocoder = new FakeGeocoder();

        var built = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(304), Karachi);
        var t = await HistoryTimelineBuilder.WithAddressesAsync(built, geocoder, CancellationToken.None);

        AssertAddsUp(t);
        var standstills = Standstills(t);
        standstills.Should().HaveCount(3);
        geocoder.Calls.Should().Be(2, "the two parking spots 60 m apart are one place");
        standstills[2].StartAddress.Should().Be(standstills[0].StartAddress);
    }

    [Fact]
    public async Task Places_LookupsAreCapped_LongestStandstillsFirst()
    {
        // More separate places than the cap; the stop at place k lasts 5 + k minutes.
        var places = HistoryTimelineBuilder.MaxPlaceLookups + 10;
        var fixes = new List<TripReplayPositionDto>();
        var t = T0;
        for (var k = 0; k < places; k++)
        {
            var lat = 31.0 + k * 0.05;
            fixes.AddRange(Stand(t, 5 + k, lat));
            t = t.AddMinutes(5 + k + 1);
            fixes.AddRange(Drive(t, 6, lat));
            t = t.AddMinutes(7);
        }
        var geocoder = new FakeGeocoder();

        var built = HistoryTimelineBuilder.Build(fixes, T0, t, Karachi);
        var named = await HistoryTimelineBuilder.WithAddressesAsync(built, geocoder, CancellationToken.None);

        AssertAddsUp(named);
        geocoder.Calls.Should().Be(HistoryTimelineBuilder.MaxPlaceLookups);
        var standstills = Standstills(named);
        standstills.Where(s => s.StartAddress is not null).Should().HaveCount(HistoryTimelineBuilder.MaxPlaceLookups);
        var shortestNamed = standstills.Where(s => s.StartAddress is not null).Min(s => s.DurationMinutes);
        var longestUnnamed = standstills.Where(s => s.StartAddress is null).Max(s => s.DurationMinutes);
        shortestNamed.Should().BeGreaterThanOrEqualTo(longestUnnamed, "the longest standstills are named first");
    }

    [Fact]
    public async Task Places_FromTheAddressCache_DoNotCountTowardsTheCap()
    {
        var places = HistoryTimelineBuilder.MaxPlaceLookups + 10;
        var fixes = new List<TripReplayPositionDto>();
        var t = T0;
        for (var k = 0; k < places; k++)
        {
            var lat = 31.0 + k * 0.05;
            fixes.AddRange(Stand(t, 5 + k, lat));
            t = t.AddMinutes(5 + k + 1);
            fixes.AddRange(Drive(t, 6, lat));
            t = t.AddMinutes(7);
        }
        var geocoder = new FakeGeocoder { Cached = true };

        var built = HistoryTimelineBuilder.Build(fixes, T0, t, Karachi);
        var named = await HistoryTimelineBuilder.WithAddressesAsync(built, geocoder, CancellationToken.None);

        Standstills(named).Should().OnlyContain(s => s.StartAddress != null, "cached answers cost no wait");
        geocoder.Calls.Should().BeGreaterThan(HistoryTimelineBuilder.MaxPlaceLookups);
    }

    [Fact]
    public async Task Places_AlreadyNamedForTheWindow_AreNotLookedUpAgain()
    {
        var fixes = StandDriveStandDriveStand(out _, out var b, out _);
        var geocoder = new FakeGeocoder();
        var known = new List<TripStopDto> { new(T0, T0, b, Lng, "7 Mall Road, Pasrur", 6) };

        var built = HistoryTimelineBuilder.Build(fixes, T0, T0.AddMinutes(50), Karachi);
        var t = await HistoryTimelineBuilder.WithAddressesAsync(built, geocoder, CancellationToken.None, known);

        geocoder.Calls.Should().Be(2);
        t.Segments[2].StartAddress.Should().Be("7 Mall Road, Pasrur");
        t.Segments[1].EndAddress.Should().Be("7 Mall Road, Pasrur");
    }

    [Fact]
    public async Task Places_KeepTheOngoingFlag()
    {
        var (fixes, _, end) = DriveThenStandUntilTheEnd();

        var built = HistoryTimelineBuilder.Build(fixes, T0, end, Karachi, nowUtc: end);
        var t = await HistoryTimelineBuilder.WithAddressesAsync(built, new FakeGeocoder(), CancellationToken.None);

        t.Segments[^1].Ongoing.Should().BeTrue();
        t.Segments[^1].StartAddress.Should().EndWith("Canal Road");
    }
}
