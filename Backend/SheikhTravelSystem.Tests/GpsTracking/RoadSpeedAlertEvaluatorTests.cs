using FluentAssertions;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.GpsTracking;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;
using SheikhTravelSystem.Infrastructure.Services.Gps;

namespace SheikhTravelSystem.Tests.GpsTracking;

public class RoadSpeedAlertEvaluatorTests
{
    [Fact]
    public void Options_DefaultMargin_Is20()
    {
        var opts = new RoadSpeedAlertOptions();
        opts.OverspeedMarginKmh.Should().Be(20m);
        opts.MinMovementMeters.Should().Be(75);
        opts.AlertDedupMinutes.Should().Be(5);
        opts.CacheTtlHours.Should().Be(24);
        opts.ApiEnabled.Should().BeTrue();
    }

    [Fact]
    public void ModuleCode_IsRoadSpeedAlerts()
    {
        TenantModuleCatalog.RoadSpeedAlertsModuleCode.Should().Be("ROAD_SPEED_ALERTS");
        TenantModuleCatalog.All.Should().Contain(m =>
            m.Code == "ROAD_SPEED_ALERTS" && m.LegacyKeys.Contains("road-speed-alerts"));
    }

    [Fact]
    public void GridCacheKey_IsStableForNearbyPointsInSameCell()
    {
        var a = RoadSpeedLimitLookupService.BuildGridCacheKey(31.52001, 74.35001, 0.0009);
        var b = RoadSpeedLimitLookupService.BuildGridCacheKey(31.52005, 74.35002, 0.0009);
        a.Should().Be(b);
        a.Should().StartWith("grid:");
    }

    [Fact]
    public void GridCacheKey_DiffersAcrossCells()
    {
        var a = RoadSpeedLimitLookupService.BuildGridCacheKey(31.52, 74.35, 0.0009);
        var b = RoadSpeedLimitLookupService.BuildGridCacheKey(31.53, 74.36, 0.0009);
        a.Should().NotBe(b);
    }

    [Theory]
    [InlineData(50, 40, 20, false)] // 50 < 40+20
    [InlineData(60, 40, 20, true)]  // 60 >= 60
    [InlineData(59.9, 40, 20, false)]
    [InlineData(61, 40, 20, true)]
    public void Threshold_RoadLimitPlusMargin(decimal speed, decimal limit, decimal margin, bool shouldAlert)
    {
        var threshold = limit + margin;
        (speed >= threshold).Should().Be(shouldAlert);
    }

    [Fact]
    public void EventType_IsRoadSpeedExceeded()
        => RoadSpeedAlertEvaluator.EventType.Should().Be("road_speed_exceeded");

    [Fact]
    public void GpsAlertSeverity_RoadSpeed_IsHigh()
        => GpsAlertSeverity.SeverityFor("road_speed_exceeded").Should().Be("high");

    [Fact]
    public void AlertTypeCatalog_IncludesRoadSpeedExceeded()
        => AlertTypeCatalog.Types.Should().Contain("road_speed_exceeded");
}
