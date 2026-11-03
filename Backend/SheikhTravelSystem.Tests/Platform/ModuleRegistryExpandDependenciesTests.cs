using FluentAssertions;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Tests.Platform;

public class ModuleRegistryExpandDependenciesTests
{
    [Fact]
    public void Expand_IncludesTransitiveEnableableDependencies()
    {
        var expanded = ModuleRegistrySeed.ExpandToggleableWithDependencies(["GPS"]);

        expanded.Should().Contain(["GPS", "FLEET", "DASHBOARD"]);
        expanded.Should().NotContain("RENTAL");
        expanded.Should().NotContain("VEHICLES");
    }

    [Fact]
    public void Expand_IgnoresComingSoonCodes()
    {
        var expanded = ModuleRegistrySeed.ExpandToggleableWithDependencies(["HR", "FLEET"]);

        expanded.Should().Contain("FLEET");
        expanded.Should().Contain("DASHBOARD");
        expanded.Should().NotContain("HR");
    }

    [Fact]
    public void IsToggleable_ComingSoonFalse_AiAndBookingsTrue()
    {
        var hr = ModuleRegistrySeed.All.Single(e => e.Code == "HR");
        ModuleRegistrySeed.IsToggleable(hr).Should().BeFalse();

        var ai = ModuleRegistrySeed.All.Single(e => e.Code == "AI");
        ModuleRegistrySeed.IsToggleable(ai).Should().BeTrue();

        var bookings = ModuleRegistrySeed.All.Single(e => e.Code == "BOOKINGS");
        ModuleRegistrySeed.IsToggleable(bookings).Should().BeTrue();
    }

    [Fact]
    public void Expand_BookingsDoesNotPullTravelRoutes()
    {
        var expanded = ModuleRegistrySeed.ExpandToggleableWithDependencies(["BOOKINGS"]);

        expanded.Should().Contain(["BOOKINGS", "DASHBOARD"]);
        expanded.Should().NotContain("TRAVEL");
    }
}
