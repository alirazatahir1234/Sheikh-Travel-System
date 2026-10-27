using FluentAssertions;
using SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

namespace SheikhTravelSystem.Tests.Trips;

public class TripAssignmentScorerTests
{
    [Fact]
    public void Score_prefers_high_rating_and_nearby_driver()
    {
        var trip = new TripAssignmentScorer.TripAssignmentTripInput(
            DateTime.UtcNow.AddHours(2),
            31.52, 74.35, 31.55, 74.40);

        var near = new TripAssignmentScorer.TripAssignmentCandidateInput(1, 4.8m, 31.521, 74.351, 80m, 3m);
        var far = new TripAssignmentScorer.TripAssignmentCandidateInput(2, 4.8m, 31.70, 74.60, 80m, 3m);

        var (nearTotal, _) = TripAssignmentScorer.Score(near, trip, null);
        var (farTotal, _) = TripAssignmentScorer.Score(far, trip, null);

        nearTotal.Should().BeGreaterThan(farTotal);
    }

    [Fact]
    public void Score_penalizes_fatigue_and_low_fuel()
    {
        var trip = new TripAssignmentScorer.TripAssignmentTripInput(
            DateTime.UtcNow.AddHours(3),
            31.52, 74.35, null, null);

        var fresh = new TripAssignmentScorer.TripAssignmentCandidateInput(1, 4m, 31.53, 74.36, 70m, 2m);
        var tired = new TripAssignmentScorer.TripAssignmentCandidateInput(2, 4m, 31.53, 74.36, 10m, 11m);

        var (freshTotal, _) = TripAssignmentScorer.Score(fresh, trip, null);
        var (tiredTotal, _) = TripAssignmentScorer.Score(tired, trip, null);

        freshTotal.Should().BeGreaterThan(tiredTotal);
    }

    [Fact]
    public void Score_uses_google_routes_when_provided()
    {
        var trip = new TripAssignmentScorer.TripAssignmentTripInput(
            DateTime.UtcNow.AddHours(1),
            31.52, 74.35, null, null);
        var cand = new TripAssignmentScorer.TripAssignmentCandidateInput(1, 4m, 31.53, 74.36, null, 4m);
        var route = new TripAssignmentScorer.RouteMetrics(12000, 900, TripAssignmentScorer.TrafficGoogleRoutes);

        var (_, factors) = TripAssignmentScorer.Score(cand, trip, route);
        factors.Should().Contain(f => f.Key == "traffic" && f.Detail.Contains("Google Routes"));
    }
}
