using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Tests.Platform;

/// <summary>
/// Phase 8 grant-on-module-enable hook: the administrator roles of a company receive an opt-in module's
/// permissions exactly when that module is switched on, never when it was already on, never on disable.
/// </summary>
public class ModulePermissionGrantTests
{
    private const int Tenant = 42;

    private readonly Mock<IPlatformRepository> _platform = new();
    private readonly Mock<IPlatformScope> _scope = new();
    private readonly Mock<ITenantModuleService> _modules = new();
    private readonly Mock<ITenantRoleSeedService> _roles = new();

    public ModulePermissionGrantTests()
    {
        _platform.Setup(p => p.EnsureTenantExistsAsync(Tenant, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private static string AnotherToggleableModule()
        => ModuleRegistrySeed.All.First(e => ModuleRegistrySeed.IsToggleable(e)
                                             && !ModulePermissionGrants.Modules.Contains(e.Code, StringComparer.OrdinalIgnoreCase)).Code;

    // ── The map ─────────────────────────────────────────────────────────────

    [Fact]
    public void Grant_map_covers_exactly_the_opt_in_modules_outside_the_role_templates()
    {
        ModulePermissionGrants.Modules.Should().BeEquivalentTo([TenantModuleCatalog.TravelAgencyModuleCode, TenantModuleCatalog.RentalModuleCode]);
        ModulePermissionGrants.PermissionsFor(TenantModuleCatalog.TravelAgencyModuleCode).Should().BeEquivalentTo(TravelPermissions.All);
        ModulePermissionGrants.PermissionsFor(TenantModuleCatalog.RentalModuleCode).Should().BeEquivalentTo(RentalPermissions.All);
        ModulePermissionGrants.PermissionsFor("FLEET").Should().BeEmpty();
        ModulePermissionGrants.Roles.Should().BeEquivalentTo(["TENANT_ADMIN", "OWNER"]);
    }

    [Fact]
    public void Every_granted_permission_is_registered_and_belongs_to_its_module()
    {
        foreach (var module in ModulePermissionGrants.Modules)
        {
            foreach (var code in ModulePermissionGrants.PermissionsFor(module))
            {
                PermissionRegistrySeed.All.Should().Contain(e => e.PermissionCode == code, $"{code} must be a registered permission");
                PermissionRegistrySeed.InferHierarchy(code).ModuleKey.Should().Be(module, $"{code} must map to {module}");
            }
        }

        // The templates stay free of opt-in module permissions (PermissionEngineTests rely on this).
        foreach (var (_, permissions) in TenantRolePermissionTemplates.StandardRoles)
            permissions.Should().NotContain(p => ModulePermissionGrants.Modules.Any(m => ModulePermissionGrants.PermissionsFor(m).Contains(p)));
    }

    [Fact]
    public void Newly_enabled_is_the_difference_restricted_to_modules_with_a_grant()
    {
        var before = new[] { "FLEET", "GPS" };
        var after = new[] { "FLEET", "GPS", TenantModuleCatalog.TravelAgencyModuleCode, AnotherToggleableModule() };

        ModulePermissionGrants.NewlyEnabled(before, after).Should().BeEquivalentTo([TenantModuleCatalog.TravelAgencyModuleCode]);
        ModulePermissionGrants.NewlyEnabled(after, after).Should().BeEmpty();
        ModulePermissionGrants.NewlyEnabled(after, before).Should().BeEmpty("disabling never produces a grant");
        ModulePermissionGrants.NewlyEnabled([], ["travel_agency"]).Should().ContainSingle("module codes compare case-insensitively");
        ModulePermissionGrants.CodesFor([TenantModuleCatalog.TravelAgencyModuleCode, TenantModuleCatalog.RentalModuleCode])
            .Should().BeEquivalentTo(TravelPermissions.All.Concat(RentalPermissions.All));
    }

    // ── SetTenantModulesCommandHandler ──────────────────────────────────────

    private SetTenantModulesCommandHandler SetHandler() => new(_platform.Object, _scope.Object, _modules.Object, _roles.Object);

    [Fact]
    public async Task Enabling_TravelNexus_grants_its_permissions_once_and_only_for_the_new_module()
    {
        var other = AnotherToggleableModule();
        _platform.Setup(p => p.GetTenantModuleCodesOrderedAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync([other]);

        var result = await SetHandler().Handle(
            new SetTenantModulesCommand(Tenant, [other, TenantModuleCatalog.TravelAgencyModuleCode]), CancellationToken.None);

        result.Success.Should().BeTrue();
        _platform.Verify(p => p.ReplaceTenantModulesAsync(Tenant,
            It.Is<IReadOnlyList<string>>(l => l.Contains(TenantModuleCatalog.TravelAgencyModuleCode) && l.Contains(other)), It.IsAny<CancellationToken>()), Times.Once);
        _roles.Verify(r => r.GrantModulePermissionsAsync(Tenant,
            It.Is<IReadOnlyCollection<string>>(m => m.Count == 1 && m.Contains(TenantModuleCatalog.TravelAgencyModuleCode)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Re_saving_an_already_enabled_module_grants_nothing()
    {
        _platform.Setup(p => p.GetTenantModuleCodesOrderedAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([TenantModuleCatalog.TravelAgencyModuleCode]);

        await SetHandler().Handle(new SetTenantModulesCommand(Tenant, [TenantModuleCatalog.TravelAgencyModuleCode]), CancellationToken.None);

        _roles.Verify(r => r.GrantModulePermissionsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Disabling_TravelNexus_keeps_the_permissions_and_grants_nothing()
    {
        var other = AnotherToggleableModule();
        _platform.Setup(p => p.GetTenantModuleCodesOrderedAsync(Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync([other, TenantModuleCatalog.TravelAgencyModuleCode]);

        var result = await SetHandler().Handle(new SetTenantModulesCommand(Tenant, [other]), CancellationToken.None);

        result.Success.Should().BeTrue();
        _roles.Verify(r => r.GrantModulePermissionsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_rejected_module_code_grants_nothing()
    {
        var result = await SetHandler().Handle(new SetTenantModulesCommand(Tenant, ["NOT_A_MODULE"]), CancellationToken.None);

        result.Success.Should().BeFalse();
        _platform.Verify(p => p.ReplaceTenantModulesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _roles.Verify(r => r.GrantModulePermissionsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── UpdateTenantCommandHandler ──────────────────────────────────────────

    private UpdateTenantCommandHandler UpdateHandler() => new(_platform.Object, _scope.Object, _modules.Object, _roles.Object);

    private static UpdateTenantCommand Update(IReadOnlyList<string>? moduleCodes)
        => new(Tenant, "Company B", null, true, null, moduleCodes, null, null, null, null, null);

    [Fact]
    public async Task Updating_a_tenant_with_a_new_opt_in_module_grants_it()
    {
        _platform.Setup(p => p.GetTenantModuleCodesOrderedAsync(Tenant, It.IsAny<CancellationToken>())).ReturnsAsync(["FLEET"]);

        var result = await UpdateHandler().Handle(Update(["FLEET", TenantModuleCatalog.RentalModuleCode]), CancellationToken.None);

        result.Success.Should().BeTrue();
        _roles.Verify(r => r.GrantModulePermissionsAsync(Tenant,
            It.Is<IReadOnlyCollection<string>>(m => m.Count == 1 && m.Contains(TenantModuleCatalog.RentalModuleCode)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Updating_a_tenant_without_touching_modules_neither_reads_nor_grants()
    {
        var result = await UpdateHandler().Handle(Update(null), CancellationToken.None);

        result.Success.Should().BeTrue();
        _platform.Verify(p => p.GetTenantModuleCodesOrderedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _roles.Verify(r => r.GrantModulePermissionsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        _modules.Verify(m => m.SyncLegacyJsonAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
