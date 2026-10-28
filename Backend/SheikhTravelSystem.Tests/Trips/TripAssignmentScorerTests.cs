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

    [Fact]
    public void Score_includes_performance_cost_utilization_and_weights_sum_to_one()
    {
        var trip = new TripAssignmentScorer.TripAssignmentTripInput(
            DateTime.UtcNow.AddHours(2),
            31.52, 74.35, null, null);
        var cand = new TripAssignmentScorer.TripAssignmentCandidateInput(
            1, 4.5m, 31.53, 74.36, 70m, 3m,
            PerformanceScore: 90,
            SafetyScore: 88,
            FuelAverageKmPerLiter: 12m,
            RecentKm7d: 120m);

        var (_, factors) = TripAssignmentScorer.Score(cand, trip, null);

        factors.Should().Contain(f => f.Key == "performance");
        factors.Should().Contain(f => f.Key == "cost");
        factors.Should().Contain(f => f.Key == "utilization");
        factors.Sum(f => f.Weight).Should().Be(1.00m);
    }

    [Fact]
    public void Score_prefers_higher_performance_and_lower_utilization()
    {
        var trip = new TripAssignmentScorer.TripAssignmentTripInput(
            DateTime.UtcNow.AddHours(2),
            31.52, 74.35, null, null);

        var strong = new TripAssignmentScorer.TripAssignmentCandidateInput(
            1, 4m, 31.53, 74.36, 70m, 3m,
            PerformanceScore: 95, SafetyScore: 95, FuelAverageKmPerLiter: 14m, RecentKm7d: 50m);
        var weak = new TripAssignmentScorer.TripAssignmentCandidateInput(
            2, 4m, 31.53, 74.36, 70m, 3m,
            PerformanceScore: 40, SafetyScore: 40, FuelAverageKmPerLiter: 5m, RecentKm7d: 1500m);

        var (strongTotal, _) = TripAssignmentScorer.Score(strong, trip, null);
        var (weakTotal, _) = TripAssignmentScorer.Score(weak, trip, null);

        strongTotal.Should().BeGreaterThan(weakTotal);
    }
}
