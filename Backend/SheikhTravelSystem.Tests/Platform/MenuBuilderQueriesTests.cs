using FluentAssertions;
using SheikhTravelSystem.Application.Features.Platform;
using Xunit;

namespace SheikhTravelSystem.Tests.Platform;

public class MenuBuilderQueriesTests
{
    [Fact]
    public void IsNavModuleEnabled_travel_fleet_operations_when_routes_or_trips()
    {
        MenuBuilderQueries.IsNavModuleEnabled("travel-fleet-operations", ["routes"]).Should().BeTrue();
        MenuBuilderQueries.IsNavModuleEnabled("travel-fleet-operations", ["trips"]).Should().BeTrue();
        MenuBuilderQueries.IsNavModuleEnabled("travel-fleet-operations", ["bookings"]).Should().BeFalse();
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
}
