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
}
