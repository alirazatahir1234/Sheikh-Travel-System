using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class AlertModuleCatalogTests
{
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
    public void PremiumAlerts_AreOptIn_NotInDefaults()
    {
        string[] premium =
        [
            "ROAD_SPEED_ALERTS",
            "ALERT_OVERSPEED",
            "ALERT_GPS_LOST",
            "ALERT_LOW_FUEL",
        ];

        foreach (var code in premium)
        {
            var type = AlertModuleCatalog.FindByModuleCode(code);
            type.Should().NotBeNull(because: $"{code} must exist");
            type!.IsPremium.Should().BeTrue(because: $"{code} is premium");
            type.DefaultEnabled.Should().BeFalse(because: $"{code} is default-off");
            AlertModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
            AlertModuleCatalog.IsPremium(code).Should().BeTrue();
            SubscriptionAddOnCatalog.IsPremiumAddOn(code).Should().BeTrue();
            TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
        }

        var road = SubscriptionAddOnCatalog.Resolve("ROAD_SPEED_ALERTS");
        road.Should().NotBeNull();
        road!.Category.Should().Be(AlertModuleCatalog.AlertsCategory);
        road.Dependencies.Should().Contain(AlertModuleCatalog.AlertsModuleCode);
    }

    [Fact]
    public void BasicAlerts_AreNotPremium_AndDefaultEnabled()
    {
        string[] basic =
        [
            "ALERT_ONLINE",
            "ALERT_OFFLINE",
            "ALERT_IGNITION_ON",
            "ALERT_IGNITION_OFF",
            "ALERT_LOW_BATTERY",
            "ALERT_POWER_CUT",
            "ALERT_GEOFENCE_ENTER",
            "ALERT_GEOFENCE_EXIT",
            "ALERT_SOS",
            "ALERT_FUEL_STATION_STOP",
        ];

        foreach (var code in basic)
        {
            var type = AlertModuleCatalog.FindByModuleCode(code);
            type.Should().NotBeNull();
            type!.IsPremium.Should().BeFalse(because: $"{code} must not carry a premium tag");
            type.DefaultEnabled.Should().BeTrue(because: $"{code} stays enabled by default");
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
    public void TenantDefaults_IncludeBasicAlerts_ExcludePremium()
    {
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain(AlertModuleCatalog.AlertsModuleCode);
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain("ALERT_SOS");
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain("ALERT_ONLINE");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ROAD_SPEED_ALERTS");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ALERT_OVERSPEED");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ALERT_GPS_LOST");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ALERT_LOW_FUEL");
        TenantModuleCatalog.All.Should().Contain(m => m.Code == AlertModuleCatalog.AlertsModuleCode);
        TenantModuleCatalog.All.Should().Contain(m => m.Code == "ROAD_SPEED_ALERTS");
    }
}
