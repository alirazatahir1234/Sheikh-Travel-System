using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class AlertModuleCatalogTests
{
    [Fact]
    public void Defaults_KeepBasicAlertsOn_ForTenantAndEveryPlan()
    {
        var basic = AlertModuleCatalog.ModuleGovernedTypes
            .Where(t => t.DefaultEnabled)
            .Select(t => t.ModuleCode)
            .Append(AlertModuleCatalog.AlertsModuleCode)
            .ToList();
        basic.Should().Contain(["ALERT_SOS", "ALERT_POWER_CUT", "ALERT_IGNITION_ON", "ALERT_IGNITION_OFF",
            "ALERT_LOW_BATTERY", "ALERT_OFFLINE", "ALERT_ONLINE"]);

        TenantModuleCatalog.DefaultModuleCodes.Should().Contain(basic);
        foreach (var plan in SubscriptionPlanCatalog.All)
        {
            plan.DefaultModuleCodes.Should().Contain(basic, because: $"{plan.SubscriptionCode} must keep basic alerts on");
            plan.DefaultModuleCodes.Should().NotContain(AlertModuleCatalog.PremiumModuleCodes);
            plan.DefaultModuleCodes.Should().NotContain("HR", because: "HR is ComingSoon");
            ModuleRegistrySeed.ExpandToggleableWithDependencies(plan.DefaultModuleCodes)
                .Should().Contain(basic);
        }
    }

    [Theory]
    [InlineData("lowBattery", "low_battery")]
    [InlineData("powerCut", "power_cut")]
    public void TraccarAlarm_MapsToAlertModuleEventType(string alarm, string expected)
    {
        Application.Features.GpsTracking.Services.TraccarAlertEventMapper
            .MapEventType("alarm", alarm).Should().Be(expected);
    }

    [Fact]
    public void Inventory_MatchesAlertTypeCatalog_AndDoesNotInventTypes()
    {
        var catalogEventTypes = AlertModuleCatalog.Types
            .Select(t => t.EventType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var type in Application.Features.GpsTracking.DTOs.AlertTypeCatalog.Types)
        {
            catalogEventTypes.Should().Contain(type,
                because: "every AlertTypeCatalog entry must map to an Alerts module child");
        }

        AlertModuleCatalog.Types.Should().HaveCount(
            Application.Features.GpsTracking.DTOs.AlertTypeCatalog.Types.Count);
    }

    [Fact]
    public void PremiumModuleAlerts_AreOptIn_NotInDefaults()
    {
        foreach (var code in new[] { "ALERT_GPS_LOST", "ALERT_LOW_FUEL" })
        {
            var type = AlertModuleCatalog.FindByModuleCode(code);
            type.Should().NotBeNull(because: $"{code} must exist");
            type!.IsPremium.Should().BeTrue(because: $"{code} is premium");
            type.DefaultEnabled.Should().BeFalse(because: $"{code} is default-off");
            type.FeatureKey.Should().BeNull();
            AlertModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
            AlertModuleCatalog.IsPremium(code).Should().BeTrue();
            SubscriptionAddOnCatalog.IsPremiumAddOn(code).Should().BeTrue();
            TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
        }
    }

    [Fact]
    public void SpeedAndGeofence_AreGpsFeatureAddOns_NotModuleDefaults()
    {
        foreach (var code in new[] { "ROAD_SPEED_ALERTS", "ALERT_OVERSPEED" })
        {
            var type = AlertModuleCatalog.FindByModuleCode(code)!;
            type.FeatureKey.Should().Be(GpsAddOnFeatureCatalog.SpeedAlerts);
            type.DefaultEnabled.Should().BeFalse();
            AlertModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
            AlertModuleCatalog.IsPremium(code).Should().BeFalse(); // module premium list excludes feature-gated
            ModuleRegistrySeed.IsEnableable(code).Should().BeFalse();
        }

        foreach (var code in new[] { "ALERT_GEOFENCE_ENTER", "ALERT_GEOFENCE_EXIT" })
        {
            var type = AlertModuleCatalog.FindByModuleCode(code)!;
            type.FeatureKey.Should().Be(GpsAddOnFeatureCatalog.Geofencing);
            type.DefaultEnabled.Should().BeFalse();
            AlertModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
            TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
        }
    }

    [Fact]
    public void BasicModuleAlerts_AreNotPremium_AndDefaultEnabled()
    {
        string[] basic =
        [
            "ALERT_ONLINE",
            "ALERT_OFFLINE",
            "ALERT_IGNITION_ON",
            "ALERT_IGNITION_OFF",
            "ALERT_LOW_BATTERY",
            "ALERT_POWER_CUT",
            "ALERT_SOS",
            "ALERT_FUEL_STATION_STOP",
        ];

        foreach (var code in basic)
        {
            var type = AlertModuleCatalog.FindByModuleCode(code);
            type.Should().NotBeNull();
            type!.IsPremium.Should().BeFalse(because: $"{code} must not carry a premium tag");
            type.DefaultEnabled.Should().BeTrue(because: $"{code} stays enabled by default");
            type.FeatureKey.Should().BeNull();
            AlertModuleCatalog.DefaultModuleCodes.Should().Contain(code);
            SubscriptionAddOnCatalog.IsPremiumAddOn(code).Should().BeFalse();
            TenantModuleCatalog.DefaultModuleCodes.Should().Contain(code);
        }

        AlertModuleCatalog.DefaultModuleCodes.Should().Contain(AlertModuleCatalog.AlertsModuleCode);
    }

    [Fact]
    public void ModuleCodeForEventType_ResolvesAliases()
    {
        AlertModuleCatalog.ModuleCodeForEventType("speed_exceeded").Should().Be("ALERT_OVERSPEED");
        AlertModuleCatalog.ModuleCodeForEventType("overspeed").Should().Be("ALERT_OVERSPEED");
        AlertModuleCatalog.ModuleCodeForEventType("vehicle_offline").Should().Be("ALERT_OFFLINE");
        AlertModuleCatalog.ModuleCodeForEventType("road_speed_exceeded")
            .Should().Be("ROAD_SPEED_ALERTS");
        AlertModuleCatalog.ModuleCodeForEventType("harsh_braking").Should().BeNull();
    }

    [Fact]
    public void TenantDefaults_IncludeBasicAlerts_ExcludePremiumAndGpsFeatureAddOns()
    {
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain(AlertModuleCatalog.AlertsModuleCode);
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain("ALERT_SOS");
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain("ALERT_ONLINE");
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain("GPS");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ROAD_SPEED_ALERTS");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ALERT_OVERSPEED");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ALERT_GEOFENCE_ENTER");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ALERT_GPS_LOST");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ALERT_LOW_FUEL");
        TenantModuleCatalog.All.Should().Contain(m => m.Code == AlertModuleCatalog.AlertsModuleCode);
        TenantModuleCatalog.All.Should().NotContain(m => m.Code == "ROAD_SPEED_ALERTS");
        TenantModuleCatalog.IsCore("GPS").Should().BeTrue();
    }
}
