using FluentAssertions;
using SheikhTravelSystem.Application.Features.Platform;
using Xunit;

namespace SheikhTravelSystem.Tests.Platform;

public class MenuBuilderQueriesTests
{
    [Fact]
    public void IsNavModuleEnabled_travel_fleet_operations_when_bookings_routes_or_trips()
    {
        MenuBuilderQueries.IsNavModuleEnabled("travel-fleet-operations", ["routes"]).Should().BeTrue();
        MenuBuilderQueries.IsNavModuleEnabled("travel-fleet-operations", ["trips"]).Should().BeTrue();
        MenuBuilderQueries.IsNavModuleEnabled("travel-fleet-operations", ["bookings"]).Should().BeTrue();
    }

    [Fact]
    public void IsNavModuleEnabled_analytics_when_reports_audit_or_gps_tracking()
    {
        MenuBuilderQueries.IsNavModuleEnabled("analytics", ["reports"]).Should().BeTrue();
        MenuBuilderQueries.IsNavModuleEnabled("analytics", ["audit-logs"]).Should().BeTrue();
        MenuBuilderQueries.IsNavModuleEnabled("analytics", ["gps-tracking"]).Should().BeTrue();
        MenuBuilderQueries.IsNavModuleEnabled("analytics", ["vehicles"]).Should().BeFalse();
    }

    [Fact]
    public void BuildUserMenu_includes_driver_performance_under_analytics_for_gps_entitlement()
    {
        var modules = new List<MenuBuilderQueries.ModuleRow>
        {
            new(1, "Analytics", "analytics", "bar_chart", 40, true, "Analytics", null, true)
        };
        var menus = new List<MenuBuilderQueries.MenuRow>
        {
            new(1, 1, null, "Driver Performance", "/gps-tracking/driver-performance", "speed", "GPS.View",
                6, true, "Driver Performance", null, "Analytics", true, "gps-tracking", "GPS", false)
        };

        var allowed = MenuBuilderQueries.BuildUserMenu(
            modules,
            menus,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GPS.View" },
            ["gps-tracking"],
            new Dictionary<string, bool>(),
            grantFullCatalog: false);

        allowed.Should().ContainSingle()
            .Which.Items.Should().ContainSingle(i => i.Route == "/gps-tracking/driver-performance");
    }

    [Fact]
    public void IsMenuItemEnabledByModule_vehicle_view_requires_fleet_entitlement()
    {
        MenuBuilderQueries.IsMenuItemEnabledByModule("Vehicle.View", ["vehicles"], "FLEET").Should().BeTrue();
        MenuBuilderQueries.IsMenuItemEnabledByModule("Vehicle.View", ["routes"], "FLEET").Should().BeFalse();
    }

    [Fact]
    public void IsMenuItemEnabledByModule_travel_permissions_require_legacy_keys()
    {
        MenuBuilderQueries.IsMenuItemEnabledByModule("Booking.View", ["routes"], null).Should().BeFalse();
        MenuBuilderQueries.IsMenuItemEnabledByModule("Trip.View", ["routes"], null).Should().BeFalse();
        MenuBuilderQueries.IsMenuItemEnabledByModule("Route.View", ["routes"], null).Should().BeTrue();

        MenuBuilderQueries.IsMenuItemEnabledByModule("Booking.View", ["bookings"], null).Should().BeTrue();
        MenuBuilderQueries.IsMenuItemEnabledByModule("Trip.View", ["trips"], null).Should().BeTrue();
        MenuBuilderQueries.IsMenuItemEnabledByModule("Booking.View", ["trips"], null).Should().BeTrue();
    }

    [Fact]
    public void BuildUserMenu_grantFullCatalog_includes_Platform_Tenants_without_permission_set()
    {
        var modules = new List<MenuBuilderQueries.ModuleRow>
        {
            new(1, "Organization", "organization", "corporate_fare", 10, true, "Organization", null, true)
        };
        var menus = new List<MenuBuilderQueries.MenuRow>
        {
            new(1, 1, null, "Companies", "/platform/tenants", "business", "Platform.Tenants.View",
                10, true, "Companies", null, null, true, null, null, false)
        };

        var denied = MenuBuilderQueries.BuildUserMenu(
            modules, menus, new HashSet<string>(), ["organization", "platform"],
            new Dictionary<string, bool>(), grantFullCatalog: false);
        denied.Should().BeEmpty();

        var allowed = MenuBuilderQueries.BuildUserMenu(
            modules, menus, new HashSet<string>(), ["organization", "platform"],
            new Dictionary<string, bool>(), grantFullCatalog: true);
        allowed.Should().ContainSingle()
            .Which.Items.Should().ContainSingle(i => i.Route == "/platform/tenants");
    }
}
