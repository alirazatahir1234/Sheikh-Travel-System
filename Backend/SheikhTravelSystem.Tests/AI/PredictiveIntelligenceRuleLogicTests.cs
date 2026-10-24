using FluentAssertions;
using SheikhTravelSystem.Application.Features.AI;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.AI;

public class PredictiveIntelligenceRuleLogicTests
{
    private static readonly DateTime AsOf = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveExpectedEnd_prefers_planned_end()
    {
        var start = AsOf.AddHours(-2);
        var end = AsOf.AddHours(1);
        PredictiveIntelligenceRuleLogic.ResolveExpectedEnd(start, end, 30)
            .Should().Be(end);
    }

    [Fact]
    public void ResolveExpectedEnd_uses_duration_when_no_planned_end()
    {
        var start = AsOf.AddHours(-1);
        PredictiveIntelligenceRuleLogic.ResolveExpectedEnd(start, null, 90)
            .Should().Be(start.AddMinutes(90));
    }

    [Fact]
    public void EvaluateTripDelay_already_delayed()
    {
        var a = PredictiveIntelligenceRuleLogic.EvaluateTripDelay(
            TripStatus.Delayed, AsOf.AddHours(-2), AsOf.AddHours(1), 120, AsOf.AddMinutes(-5), AsOf);
        a.Kind.Should().Be(TripDelayRiskKind.AlreadyMarkedDelayed);
        a.IsAtRisk.Should().BeTrue();
        a.GpsStale.Should().BeFalse();
    }

    [Fact]
    public void EvaluateTripDelay_late_start()
    {
        var planned = AsOf.AddMinutes(-40);
        var a = PredictiveIntelligenceRuleLogic.EvaluateTripDelay(
            TripStatus.Scheduled, planned, planned.AddHours(2), null, null, AsOf);
        a.Kind.Should().Be(TripDelayRiskKind.LateStart);
        a.GpsStale.Should().BeTrue();
    }

    [Fact]
    public void EvaluateTripDelay_past_expected_end_with_stale_gps_flag()
    {
        var start = AsOf.AddHours(-3);
        var end = AsOf.AddMinutes(-20);
        var a = PredictiveIntelligenceRuleLogic.EvaluateTripDelay(
            TripStatus.Enroute, start, end, null, AsOf.AddHours(-2), AsOf);
        a.Kind.Should().Be(TripDelayRiskKind.PastExpectedEnd);
        a.GpsStale.Should().BeTrue();
        a.IsAtRisk.Should().BeTrue();
    }

    [Fact]
    public void EvaluateTripDelay_insufficient_schedule_on_active_trip()
    {
        var a = PredictiveIntelligenceRuleLogic.EvaluateTripDelay(
            TripStatus.Started, AsOf.AddHours(-1), null, null, AsOf.AddMinutes(-2), AsOf);
        a.Kind.Should().Be(TripDelayRiskKind.InsufficientSchedule);
        a.IsAtRisk.Should().BeFalse();
    }

    [Fact]
    public void EvaluateTripDelay_on_schedule()
    {
        var start = AsOf.AddMinutes(-30);
        var end = AsOf.AddHours(1);
        var a = PredictiveIntelligenceRuleLogic.EvaluateTripDelay(
            TripStatus.Enroute, start, end, null, AsOf.AddMinutes(-1), AsOf);
        a.Kind.Should().Be(TripDelayRiskKind.None);
        a.GpsStale.Should().BeFalse();
    }

    [Fact]
    public void Fuel_sample_and_l100km_thresholds()
    {
        PredictiveIntelligenceRuleLogic.HasSufficientFuelSample(2, 10, 200).Should().BeFalse();
        PredictiveIntelligenceRuleLogic.HasSufficientFuelSample(3, 7, 100).Should().BeTrue();

        PredictiveIntelligenceRuleLogic.ComputeLitersPer100Km(50, 1000, 1050).Should().BeNull(); // 50 km < 100
        PredictiveIntelligenceRuleLogic.ComputeLitersPer100Km(40, 1000, 1200)
            .Should().Be(20m); // 40 L / 200 km * 100
    }

    [Fact]
    public void Fuel_anomaly_vs_median_and_wording()
    {
        var median = PredictiveIntelligenceRuleLogic.Median([10m, 12m, 14m]);
        median.Should().Be(12m);

        PredictiveIntelligenceRuleLogic.IsFuelRateAnomaly(18m, 12m).Should().BeTrue();
        PredictiveIntelligenceRuleLogic.IsFuelRateAnomaly(17m, 12m).Should().BeFalse();

        var wording = PredictiveIntelligenceRuleLogic.FuelInvestigationWording("Van A", 20m, 12m, 5);
        wording.Should().Contain("Investigation");
        wording.Should().Contain("Not proof of misconduct");
    }

    [Fact]
    public void Gps_stale_when_missing_or_old()
    {
        PredictiveIntelligenceRuleLogic.IsGpsStale(null, AsOf).Should().BeTrue();
        PredictiveIntelligenceRuleLogic.IsGpsStale(AsOf.AddMinutes(-31), AsOf).Should().BeTrue();
        PredictiveIntelligenceRuleLogic.IsGpsStale(AsOf.AddMinutes(-10), AsOf).Should().BeFalse();
    }
}
