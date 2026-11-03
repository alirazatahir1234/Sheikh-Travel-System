using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class GpsAddOnFeatureCatalogTests
{
    [Fact]
    public void Catalog_Contains_Geofencing_SpeedAlerts_RoutePlayback()
    {
        GpsAddOnFeatureCatalog.FeatureKeys.Should().BeEquivalentTo(
            ["geofencing", "speed-alerts", "route-playback"]);
        GpsAddOnFeatureCatalog.IsGpsAddOn("geofencing").Should().BeTrue();
        GpsAddOnFeatureCatalog.IsGpsAddOn("gps-tracking").Should().BeFalse();
    }

    [Fact]
    public void Gps_IsCoreAlwaysOn_NotToggleable()
    {
        TenantModuleCatalog.IsCore("GPS").Should().BeTrue();
        TenantModuleCatalog.CoreModuleCodes.Should().Contain("GPS");
        ModuleRegistrySeed.IsEnableable("GPS").Should().BeTrue();
        var gps = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == "GPS").Subject;
        ModuleRegistrySeed.IsToggleable(gps).Should().BeFalse();
    }

    [Fact]
    public void SpeedAndGeofenceAlertTypes_AreFeatureGoverned_NotEnableableModules()
    {
        foreach (var code in new[] { "ALERT_OVERSPEED", "ROAD_SPEED_ALERTS", "ALERT_GEOFENCE_ENTER", "ALERT_GEOFENCE_EXIT" })
        {
            ModuleRegistrySeed.IsEnableable(code).Should().BeFalse();
            TenantModuleCatalog.All.Should().NotContain(m => m.Code == code);
            AlertModuleCatalog.FindByModuleCode(code)!.FeatureKey.Should().NotBeNullOrWhiteSpace();
        }

        AlertModuleCatalog.FindByEventType("overspeed")!.FeatureKey
            .Should().Be(GpsAddOnFeatureCatalog.SpeedAlerts);
        AlertModuleCatalog.FindByEventType("geofence_enter")!.FeatureKey
            .Should().Be(GpsAddOnFeatureCatalog.Geofencing);
    }

    [Fact]
    public void FeatureRegistry_GpsAddOns_AreToggleable_ModuleLegacy_AreNot()
    {
        foreach (var key in GpsAddOnFeatureCatalog.FeatureKeys)
        {
            var entry = FeatureRegistrySeed.Find(key);
            entry.Should().NotBeNull();
            FeatureRegistrySeed.IsTenantFeatureToggle(entry!).Should().BeTrue();
        }

        var gpsTracking = FeatureRegistrySeed.Find("gps-tracking");
        gpsTracking.Should().NotBeNull();
        FeatureRegistrySeed.IsTenantFeatureToggle(gpsTracking!).Should().BeFalse();
        TenantModuleCatalog.IsModuleLegacyFeatureKey("gps-tracking").Should().BeTrue();
    }
}
