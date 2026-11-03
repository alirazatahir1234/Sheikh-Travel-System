using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class ModuleRegistrySeedTests
{
    /// <summary>
    /// Active modules that ship with a non-null ERP route. Keep in sync when adding real routes.
    /// </summary>
    private static readonly HashSet<string> ActiveModulesWithRoutes =
        new[]
        {
            "DASHBOARD", "FLEET", "GPS", "TRAVEL", "CRM", "FINANCE", "ANALYTICS", "ACCESS",
            "PLATFORM", "BOOKINGS", "TRIPS", "DRIVERS", "VEHICLES", "MAINTENANCE", "FUEL",
            "PAYMENTS", "INVOICES", "SETTINGS", "AI", "RENTAL",
        }
        .Concat(AlertModuleCatalog.AllModuleCodes)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void AiAssistant_IsActiveEnableable_NotInDefaultModuleCodes()
    {
        var ai = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == "AI").Subject;
        ai.Status.Should().Be("Active");
        ai.DisplayName.Should().Be("SheikhGo AI Assistant");
        ModuleRegistrySeed.IsEnableable("AI").Should().BeTrue();
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("AI");
        TenantModuleCatalog.All.Should().Contain(m => m.Code == "AI");
    }

    [Fact]
    public void PremiumModuleAlertChildren_AreActiveEnableable_NotInDefaults()
    {
        // Speed / geofence moved to GpsAddOnFeatureCatalog TenantFeatures.
        foreach (var code in new[] { "ALERT_GPS_LOST", "ALERT_LOW_FUEL" })
        {
            var mod = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == code).Subject;
            mod.Status.Should().Be("Active");
            mod.Category.Should().Be(AlertModuleCatalog.AlertsCategory);
            mod.Description.Should().StartWith("Premium");
            mod.Dependencies.Should().Contain(AlertModuleCatalog.AlertsModuleCode);
            ModuleRegistrySeed.IsEnableable(code).Should().BeTrue();
            ModuleRegistrySeed.IsToggleable(mod).Should().BeTrue();
            SubscriptionAddOnCatalog.IsPremiumAddOn(code).Should().BeTrue();
            AlertModuleCatalog.IsPremium(code).Should().BeTrue();
            TenantModuleCatalog.DefaultModuleCodes.Should().NotContain(code);
        }

        ModuleRegistrySeed.All.Should().NotContain(e => e.Code == "ROAD_SPEED_ALERTS");
        ModuleRegistrySeed.All.Should().NotContain(e => e.Code == "ALERT_OVERSPEED");

        var expanded = ModuleRegistrySeed.ExpandToggleableWithDependencies(["ALERT_GPS_LOST"]);
        expanded.Should().Contain("ALERT_GPS_LOST");
        expanded.Should().Contain(AlertModuleCatalog.AlertsModuleCode);
        expanded.Should().Contain("GPS");
        expanded.Should().Contain("FLEET");
        expanded.Should().Contain("DASHBOARD");
    }

    [Fact]
    public void Alerts_IsMajorEnableableModule_WithPerTypeChildren()
    {
        var alerts = ModuleRegistrySeed.All.Should()
            .ContainSingle(e => e.Code == AlertModuleCatalog.AlertsModuleCode).Subject;
        alerts.Category.Should().Be(AlertModuleCatalog.AlertsCategory);
        alerts.Dependencies.Should().Contain("GPS");
        ModuleRegistrySeed.IsEnableable(AlertModuleCatalog.AlertsModuleCode).Should().BeTrue();
        TenantModuleCatalog.DefaultModuleCodes.Should().Contain(AlertModuleCatalog.AlertsModuleCode);

        foreach (var type in AlertModuleCatalog.ModuleGovernedTypes)
        {
            var child = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == type.ModuleCode).Subject;
            child.Category.Should().Be(AlertModuleCatalog.AlertsCategory);
            child.Dependencies.Should().Contain(AlertModuleCatalog.AlertsModuleCode);
            ModuleRegistrySeed.IsEnableable(type.ModuleCode).Should().BeTrue();
        }
    }

    [Fact]
    public void Bookings_IsActiveEnableable_TravelIsRoutesOnly()
    {
        var travel = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == "TRAVEL").Subject;
        travel.LegacyKeys.Should().BeEquivalentTo(["routes"]);
        travel.DisplayName.Should().Be("Routes");
        ModuleRegistrySeed.IsEnableable("TRAVEL").Should().BeTrue();

        var bookings = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == "BOOKINGS").Subject;
        bookings.Status.Should().Be("Active");
        bookings.DisplayName.Should().Be("Bookings & Trips");
        bookings.LegacyKeys.Should().BeEquivalentTo(["bookings", "trips"]);
        ModuleRegistrySeed.IsEnableable("BOOKINGS").Should().BeTrue();
        ModuleRegistrySeed.IsToggleable(bookings).Should().BeTrue();
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("BOOKINGS");
    }

    [Fact]
    public void Trips_IsCatalogOnly_UnderBookings()
    {
        var trips = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == "TRIPS").Subject;
        trips.Dependencies.Should().Contain("BOOKINGS");
        ModuleRegistrySeed.IsEnableable("TRIPS").Should().BeFalse();
    }

    [Fact]
    public void Rental_IsActiveEnableable_WithRoute_HrRemainsComingSoon()
    {
        var rental = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == "RENTAL").Subject;
        rental.Status.Should().Be("Active");
        rental.Route.Should().Be("/rental");
        rental.LegacyKeys.Should().BeEquivalentTo(["rental"]);
        ModuleRegistrySeed.IsEnableable("RENTAL").Should().BeTrue();
        ModuleRegistrySeed.IsToggleable(rental).Should().BeTrue();
        TenantModuleCatalog.DefaultModuleCodes.Should().NotContain("RENTAL");

        var hr = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == "HR").Subject;
        hr.Status.Should().Be("ComingSoon");
        hr.Route.Should().BeNull();
    }

    [Fact]
    public void ActiveModules_WithNonNullRoutes_MatchKnownAllowlist()
    {
        var activeRouted = ModuleRegistrySeed.All
            .Where(e => e.Status == "Active" && e.Route is not null)
            .Select(e => e.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        activeRouted.Should().BeEquivalentTo(ActiveModulesWithRoutes);
    }
}
