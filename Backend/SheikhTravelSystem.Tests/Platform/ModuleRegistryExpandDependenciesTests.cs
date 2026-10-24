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
        var expanded = ModuleRegistrySeed.ExpandToggleableWithDependencies(["RENTAL", "FLEET"]);

        expanded.Should().Contain("FLEET");
        expanded.Should().Contain("DASHBOARD");
        expanded.Should().NotContain("RENTAL");
    }

    [Fact]
    public void IsToggleable_ComingSoonFalse_AiTrue()
    {
        var rental = ModuleRegistrySeed.All.Single(e => e.Code == "RENTAL");
        ModuleRegistrySeed.IsToggleable(rental).Should().BeFalse();

        var ai = ModuleRegistrySeed.All.Single(e => e.Code == "AI");
        ModuleRegistrySeed.IsToggleable(ai).Should().BeTrue();
    }
}
