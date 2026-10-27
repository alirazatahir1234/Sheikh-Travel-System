using FluentAssertions;
using SheikhTravelSystem.Application.Features.AccidentRisk.Services;

namespace SheikhTravelSystem.Tests.AccidentRisk;

public class AccidentRiskCalculatorTests
{
    [Fact]
    public void Driver_high_accidents_and_speed_hits_high_band()
    {
        var row = AccidentRiskCalculator.BuildDriver(1, "Ali", accidentCount: 2, speedViolationCount: 10, fatigueEventCount: 2);
        row.RiskScore.Should().BeGreaterThanOrEqualTo(AccidentRiskCalculator.HighRiskThreshold);
        row.IsHighRisk.Should().BeTrue();
        row.Factors.Should().Contain(f => f.Key == "accidents" && f.Points > 0);
    }

    [Fact]
    public void Driver_clean_record_stays_low()
    {
        var row = AccidentRiskCalculator.BuildDriver(2, "Sara", 0, 0, 0);
        row.RiskBand.Should().Be("Low");
        row.IsHighRisk.Should().BeFalse();
    }

    [Fact]
    public void Route_long_distance_adds_difficulty_points()
    {
        var row = AccidentRiskCalculator.BuildRoute(5, "Riyadh → Jeddah", 950m, 12, 0, 0);
        row.Factors.First(f => f.Key == "route_difficulty").Points.Should().Be(15);
    }
}
