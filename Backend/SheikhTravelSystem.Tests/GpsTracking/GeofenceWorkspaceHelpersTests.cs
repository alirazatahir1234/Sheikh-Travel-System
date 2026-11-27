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

    [Fact]
    public void Vehicle_id_lists_drop_blanks_and_junk()
    {
        GeofenceIdList.Parse("3, 4,x,,4,-1").Should().Equal(3, 4);
        GeofenceIdList.Parse(" ").Should().BeNull();
    }
}
