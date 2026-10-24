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
}
