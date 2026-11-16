using FluentAssertions;
using SheikhTravelSystem.Application.Common.Multitenancy;

namespace SheikhTravelSystem.Tests.Tenants;

public class SwitchableTenantStatusTests
{
    [Theory]
    [InlineData(true, "Active", "Active", SwitchableTenantStatus.Active)]
    [InlineData(true, "Active", "PastDue", SwitchableTenantStatus.Active)]
    [InlineData(true, "Suspended", "Active", SwitchableTenantStatus.Suspended)]
    [InlineData(true, "Active", "Suspended", SwitchableTenantStatus.Suspended)]
    [InlineData(false, "Active", "Suspended", SwitchableTenantStatus.Suspended)]
    [InlineData(false, "Active", "Expired", SwitchableTenantStatus.Expired)]
    [InlineData(true, "Expired", "Active", SwitchableTenantStatus.Expired)]
    [InlineData(false, "Active", "Active", SwitchableTenantStatus.Deactivated)]
    [InlineData(false, null, null, SwitchableTenantStatus.Deactivated)]
    public void Resolve_maps_expected_status(
        bool isActive, string? tenantStatus, string? subscriptionStatus, string expected)
    {
        SwitchableTenantStatus.Resolve(isActive, tenantStatus, subscriptionStatus)
            .Should().Be(expected);
    }

    [Fact]
    public void Suspended_and_Expired_remain_in_switchable_status_set()
    {
        var statuses = new[]
        {
            SwitchableTenantStatus.Resolve(false, "Suspended", "Suspended"),
            SwitchableTenantStatus.Resolve(false, "Active", "Expired"),
            SwitchableTenantStatus.Resolve(false, "Active", "Active"),
            SwitchableTenantStatus.Resolve(true, "Active", "Active"),
        };

        statuses.Should().Contain(SwitchableTenantStatus.Suspended);
        statuses.Should().Contain(SwitchableTenantStatus.Expired);
        statuses.Should().Contain(SwitchableTenantStatus.Deactivated);
        statuses.Should().Contain(SwitchableTenantStatus.Active);
    }
}
