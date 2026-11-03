using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class SubscriptionAddOnCatalogTests
{
    [Fact]
    public void Catalog_IncludesAiAndPremiumAlertAddOns_NotGpsFeatureAddOns()
    {
        SubscriptionAddOnCatalog.ModuleCodes.Should().Contain(TenantModuleCatalog.AiAssistantModuleCode);
        SubscriptionAddOnCatalog.ModuleCodes.Should().NotContain(TenantModuleCatalog.RoadSpeedAlertsModuleCode);
        SubscriptionAddOnCatalog.ModuleCodes.Should().NotContain("ALERT_OVERSPEED");
        SubscriptionAddOnCatalog.ModuleCodes.Should().Contain("ALERT_GPS_LOST");
        SubscriptionAddOnCatalog.ModuleCodes.Should().Contain("ALERT_LOW_FUEL");

        SubscriptionAddOnCatalog.IsPremiumAddOn("AI").Should().BeTrue();
        SubscriptionAddOnCatalog.IsPremiumAddOn("ROAD_SPEED_ALERTS").Should().BeFalse();
        SubscriptionAddOnCatalog.IsPremiumAddOn("ALERT_OVERSPEED").Should().BeFalse();
        SubscriptionAddOnCatalog.IsPremiumAddOn("ALERT_GPS_LOST").Should().BeTrue();
        SubscriptionAddOnCatalog.IsPremiumAddOn("ALERT_LOW_FUEL").Should().BeTrue();
        SubscriptionAddOnCatalog.IsPremiumAddOn("ALERT_SOS").Should().BeFalse();
        SubscriptionAddOnCatalog.IsPremiumAddOn("ALERT_ONLINE").Should().BeFalse();
        SubscriptionAddOnCatalog.IsPremiumAddOn("GPS").Should().BeFalse();
        SubscriptionAddOnCatalog.IsPremiumAddOn("BOOKINGS").Should().BeFalse();
        GpsAddOnFeatureCatalog.IsGpsAddOn("speed-alerts").Should().BeTrue();
    }

    [Fact]
    public void PremiumModuleAlerts_AreNotInAnyPlanDefaultModules_AndHaveNoInventedPrice()
    {
        string[] premium = ["ALERT_GPS_LOST", "ALERT_LOW_FUEL"];

        foreach (var code in premium)
        {
            foreach (var plan in SubscriptionPlanCatalog.All)
                plan.DefaultModuleCodes.Should().NotContain(code);

            TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(code);

            var entry = SubscriptionAddOnCatalog.Resolve(code);
            entry.Should().NotBeNull();
            entry!.MonthlyAmount.Should().BeNull();
            entry.CurrencyCode.Should().Be("PKR");
            entry.Dependencies.Should().Contain(AlertModuleCatalog.AlertsModuleCode);
            entry.Category.Should().Be(AlertModuleCatalog.AlertsCategory);
            AlertModuleCatalog.IsPremium(code).Should().BeTrue();
        }

        SubscriptionAddOnCatalog.Resolve("ROAD_SPEED_ALERTS").Should().BeNull();
    }

    [Fact]
    public void Ai_AlsoHasNullPlaceholderPrice()
    {
        var ai = SubscriptionAddOnCatalog.Resolve("AI");
        ai.Should().NotBeNull();
        ai!.MonthlyAmount.Should().BeNull();
    }
}
