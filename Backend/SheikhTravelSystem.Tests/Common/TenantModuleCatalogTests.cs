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
    public void RoadSpeedAlerts_IsOptIn_NotInDefaults()
    {
        TenantModuleCatalog.RoadSpeedAlertsModuleCode.Should().Be("ROAD_SPEED_ALERTS");
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(
            TenantModuleCatalog.RoadSpeedAlertsModuleCode);
        TenantModuleCatalog.IsEntitled("ROAD_SPEED_ALERTS", TenantModuleCatalog.LegacyKeysFromCodes(
            TenantModuleCatalog.DefaultModuleCodes)).Should().BeFalse();
        TenantModuleCatalog.IsEntitled("ROAD_SPEED_ALERTS", ["road-speed-alerts"]).Should().BeTrue();
        TenantModuleCatalog.LegacyKeysFromCodes(["ROAD_SPEED_ALERTS"])
            .Should().BeEquivalentTo(["road-speed-alerts"]);
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
