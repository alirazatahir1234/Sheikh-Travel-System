using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Common;

public class ModuleRegistrySeedTests
{
    /// <summary>
    /// Active modules that ship with a non-null ERP route. Keep in sync when adding real routes.
    /// </summary>
    private static readonly HashSet<string> ActiveModulesWithRoutes =
    [
        "DASHBOARD", "FLEET", "GPS", "TRAVEL", "CRM", "FINANCE", "ANALYTICS", "ACCESS",
        "PLATFORM", "BOOKINGS", "TRIPS", "DRIVERS", "VEHICLES", "MAINTENANCE", "FUEL",
        "PAYMENTS", "INVOICES", "SETTINGS", "AI",
    ];

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
    public void RentalAndHr_AreComingSoon_WithNullRoute()
    {
        var rental = ModuleRegistrySeed.All.Should().ContainSingle(e => e.Code == "RENTAL").Subject;
        rental.Status.Should().Be("ComingSoon");
        rental.Route.Should().BeNull();

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
