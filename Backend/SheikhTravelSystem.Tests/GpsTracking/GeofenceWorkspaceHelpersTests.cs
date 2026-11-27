using FluentAssertions;
using SheikhTravelSystem.Application.Features.GpsTracking.Geofences;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class GeofenceWorkspaceHelpersTests
{
    private static readonly DateTime DayStart = new(2026, 11, 26, 19, 0, 0, DateTimeKind.Utc); // 00:00 PKT

    [Fact]
    public void Occupancy_counts_each_vehicle_once_per_hour_it_was_inside()
    {
        var visits = new List<(int, DateTime, DateTime?, bool, int?)>
        {
            (1, DayStart.AddHours(8).AddMinutes(10), DayStart.AddHours(10).AddMinutes(5), false, null), // 08–10
            (2, DayStart.AddHours(9).AddMinutes(30), null, false, null),                                 // 09–now
            (1, DayStart.AddHours(9).AddMinutes(50), DayStart.AddHours(9).AddMinutes(55), false, null),  // same vehicle again
        };

        var (occupancy, _) = GeofenceWorkspaceRepository.HourlyOccupancy(visits, DayStart, nowUtc: DayStart.AddHours(11).AddMinutes(30));

        occupancy.Should().HaveCount(24);
        occupancy[7].Should().Be(0);
        occupancy[8].Should().Be(1);
        occupancy[9].Should().Be(2, "vehicle 1 counts once even with two visits that hour");
        occupancy[10].Should().Be(2);
        occupancy[11].Should().Be(1, "only the open visit is still inside");
        occupancy[12].Should().Be(0, "hours after now are empty");
    }

    [Fact]
    public void Dwell_breach_marks_the_hour_the_limit_was_passed()
    {
        var visits = new List<(int, DateTime, DateTime?, bool, int?)>
        {
            (1, DayStart.AddHours(13).AddMinutes(40), null, true, 30), // breach at 14:10
        };

        var (_, breach) = GeofenceWorkspaceRepository.HourlyOccupancy(visits, DayStart, DayStart.AddHours(16));

        breach[14].Should().BeTrue();
        breach.Count(b => b).Should().Be(1);
    }

    [Fact]
    public void Test_with_history_counts_outside_to_inside_transitions()
    {
        var shape = new TestGeofenceShapeDto { AreaType = "circle", CenterLat = 31.5, CenterLng = 74.3, RadiusMeters = 100 };
        var t0 = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);
        var points = new List<(int, double, double, DateTime)>
        {
            (7, 31.5030, 74.3, t0),                  // outside (~330 m)
            (7, 31.5001, 74.3, t0.AddMinutes(1)),    // inside → entry 1
            (7, 31.5002, 74.3, t0.AddMinutes(2)),    // still inside
            (7, 31.5001, 74.3, t0.AddMinutes(40)),   // gap > 10 min: it left and came back → entry 2
            (8, 31.5000, 74.3, t0),                  // first fix inside → entry 3
        };

        var (entries, vehicles) = GeofenceWorkspaceRepository.CountEntries(points, shape);

        entries.Should().Be(3);
        vehicles.Should().Be(2);
    }

    private static readonly TimeZoneInfo Pkt = TimeZoneInfo.CreateCustomTimeZone("PKT", TimeSpan.FromHours(5), "PKT", "PKT");

    private static GeofenceWorkspaceRepository.GroupableVisit Visit(
        int vehicle, DateTime entered, DateTime? exited, int? driver = null, int idleSeconds = 0, int breaches = 0, int geofence = 1)
        => new()
        {
            VehicleId = vehicle, VehicleLabel = $"LEA {vehicle}", DriverId = driver, DriverName = driver is null ? null : $"Driver {driver}",
            GeofenceId = geofence, GeofenceName = $"Zone {geofence}", EnteredUtc = entered, ExitedUtc = exited,
            IdleSeconds = idleSeconds, Breaches = breaches
        };

    [Fact]
    public void Grouping_by_vehicle_adds_up_minutes_the_way_the_totals_do()
    {
        var now = DayStart.AddHours(12);
        var visits = new[]
        {
            Visit(1, DayStart.AddHours(8), DayStart.AddHours(9).AddMinutes(30), idleSeconds: 300, breaches: 1), // 90 min
            Visit(1, DayStart.AddHours(10), null),                                                              // open: 120 min to now
            Visit(2, DayStart.AddHours(9), DayStart.AddHours(9).AddMinutes(20)),                                // 20 min
        };

        var groups = GeofenceWorkspaceRepository.GroupVisits(visits, GeofenceVisitGroupBy.Vehicle, Pkt, DayStart, now);

        groups.Should().HaveCount(2);
        var first = groups[0];
        first.Key.Should().Be("1");
        first.Label.Should().Be("LEA 1");
        first.Visits.Should().Be(2);
        first.InsideNow.Should().Be(1);
        first.TotalMinutes.Should().Be(210);
        first.AvgMinutes.Should().Be(90, "only completed visits count towards the average");
        first.IdleMinutes.Should().Be(5);
        first.Breaches.Should().Be(1);
        groups[1].TotalMinutes.Should().Be(20, "groups are ordered by time on site, longest first");
    }

    [Fact]
    public void Grouping_by_day_uses_the_company_day_and_puts_earlier_visits_on_the_first_day()
    {
        var rangeStart = DayStart; // 27 Nov 00:00 PKT
        var visits = new[]
        {
            Visit(1, new DateTime(2026, 11, 27, 20, 30, 0, DateTimeKind.Utc), null),              // 28 Nov 01:30 PKT
            Visit(2, rangeStart.AddHours(-3), rangeStart.AddHours(1)),                              // started on 26 Nov
            Visit(3, rangeStart.AddHours(6), rangeStart.AddHours(7)),                               // 27 Nov
        };

        var groups = GeofenceWorkspaceRepository.GroupVisits(
            visits, GeofenceVisitGroupBy.Day, Pkt, rangeStart, new DateTime(2026, 11, 27, 21, 0, 0, DateTimeKind.Utc));

        groups.Select(g => g.Key).Should().Equal("2026-11-27", "2026-11-28");
        groups[0].Visits.Should().Be(2);
        groups[0].Label.Should().Be("2026-11-27");
    }

    [Fact]
    public void Grouping_by_driver_keeps_visits_without_a_driver_together()
    {
        var visits = new[]
        {
            Visit(1, DayStart.AddHours(1), DayStart.AddHours(2)),
            Visit(2, DayStart.AddHours(3), DayStart.AddHours(4)),
            Visit(3, DayStart.AddHours(5), DayStart.AddHours(5).AddMinutes(10), driver: 9),
        };

        var groups = GeofenceWorkspaceRepository.GroupVisits(visits, GeofenceVisitGroupBy.Driver, Pkt, DayStart, DayStart.AddHours(6));

        groups.Should().ContainSingle(g => g.Key == "none" && g.Label == "No driver" && g.Visits == 2 && g.Vehicles == 2);
        groups.Should().ContainSingle(g => g.Key == "9" && g.Label == "Driver 9");
    }

    [Fact]
    public void Vehicle_id_lists_drop_blanks_and_junk()
    {
        GeofenceIdList.Parse("3, 4,x,,4,-1").Should().Equal(3, 4);
        GeofenceIdList.Parse(" ").Should().BeNull();
    }
}
