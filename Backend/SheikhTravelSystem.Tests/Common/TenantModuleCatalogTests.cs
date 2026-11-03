using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class TenantModuleCatalogTests
{
    [Fact]
    public void AiAssistant_IsPremium_NotInDefaultsOrPlanCodes()
    {
        TenantModuleCatalog.AiAssistantModuleCode.Should().Be("AI");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(
            TenantModuleCatalog.AiAssistantModuleCode);
        TenantModuleCatalog.IsEntitled("AI", TenantModuleCatalog.LegacyKeysFromCodes(
            TenantModuleCatalog.DefaultModuleCodes)).Should().BeFalse();
    }

    [Fact]
    public void IsEntitled_Ai_RequiresLegacyKeyAi()
    {
        TenantModuleCatalog.IsEntitled("AI", ["dashboard", "vehicles"]).Should().BeFalse();
        TenantModuleCatalog.IsEntitled("AI", ["dashboard", "ai"]).Should().BeTrue();
        TenantModuleCatalog.IsEntitled("ai", ["ai"]).Should().BeTrue();
    }

    [Fact]
    public void PremiumModuleAlerts_NotInDefaults_BasicAlertsAre_GpsAddOnsAreFeatures()
    {
        TenantModuleCatalog.RoadSpeedAlertsModuleCode.Should().Be("ROAD_SPEED_ALERTS");
        TenantModuleCatalog.AlertsModuleCode.Should().Be("ALERTS");
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain(TenantModuleCatalog.AlertsModuleCode);
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain("ALERT_SOS");
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain("ALERT_ONLINE");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("ALERT_GEOFENCE_ENTER");
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain("GPS");
        TenantModuleCatalog.IsCore("GPS").Should().BeTrue();

        foreach (var code in new[] { "ALERT_GPS_LOST", "ALERT_LOW_FUEL" })
        {
            TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
            SubscriptionAddOnCatalog.IsPremiumAddOn(code).Should().BeTrue();
            TenantModuleCatalog.IsEntitled(code, TenantModuleCatalog.LegacyKeysFromCodes(
                TenantModuleCatalog.DefaultModuleCodes)).Should().BeFalse();
        }

        // Speed / geofence are TenantFeatures — not TenantModules entitlements.
        TenantModuleCatalog.All.Should().NotContain(m => m.Code == "ROAD_SPEED_ALERTS");
        TenantModuleCatalog.IsEntitled("ROAD_SPEED_ALERTS", ["road-speed-alerts"]).Should().BeFalse();
        TenantModuleCatalog.IsEntitled("ALERTS", TenantModuleCatalog.LegacyKeysFromCodes(
            TenantModuleCatalog.DefaultModuleCodes)).Should().BeTrue();
        GpsAddOnFeatureCatalog.IsGpsAddOn("speed-alerts").Should().BeTrue();
        GpsAddOnFeatureCatalog.IsGpsAddOn("geofencing").Should().BeTrue();
    }

    [Fact]
    public void Bookings_IsOptIn_SeparateFromTravelRoutes()
    {
        TenantModuleCatalog.BookingsModuleCode.Should().Be("BOOKINGS");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(
            TenantModuleCatalog.BookingsModuleCode);
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain(
            TenantModuleCatalog.TravelRoutesModuleCode);

        var defaults = TenantModuleCatalog.LegacyKeysFromCodes(TenantModuleCatalog.DefaultModuleCodes);
        defaults.Should().Contain("routes");
        defaults.Should().NotContain("bookings");
        defaults.Should().NotContain("trips");
        TenantModuleCatalog.IsEntitled("BOOKINGS", defaults).Should().BeFalse();
        TenantModuleCatalog.IsEntitled("TRAVEL", defaults).Should().BeTrue();
    }

    [Fact]
    public void LegacyKeys_TravelIsRoutesOnly_BookingsOwnsBookingsAndTrips()
    {
        TenantModuleCatalog.LegacyKeysFromCodes(["TRAVEL"]).Should().BeEquivalentTo(["routes"]);
        TenantModuleCatalog.LegacyKeysFromCodes(["BOOKINGS"])
            .Should().BeEquivalentTo(["bookings", "trips"]);

        TenantModuleCatalog.CodesFromLegacyKeys(["bookings", "routes", "trips"])
            .Should().BeEquivalentTo(["TRAVEL", "BOOKINGS"]);
    }
}
