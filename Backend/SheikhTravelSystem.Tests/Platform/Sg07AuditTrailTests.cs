using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Tests.Platform;

public class Sg07AuditTrailTests
{
    [Fact]
    public void ResolveSearchScope_AllCompanies_When_AuditReadAll_And_No_Tenant()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns((int?)null);
        var scope = new Mock<IPlatformScope>();
        scope.Setup(s => s.HasPermission(PlatformPermissions.TenantsAuditReadAll)).Returns(true);

        var result = SearchAuditEventsQueryHandler.ResolveSearchScope(null, tenant.Object, scope.Object);

        result.AllCompanies.Should().BeTrue();
        result.TenantId.Should().BeNull();
    }

    [Fact]
    public void ResolveSearchScope_Throws_Without_Tenant_Or_AuditReadAll()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns((int?)null);
        var scope = new Mock<IPlatformScope>();
        scope.Setup(s => s.HasPermission(PlatformPermissions.TenantsAuditReadAll)).Returns(false);

        var act = () => SearchAuditEventsQueryHandler.ResolveSearchScope(null, tenant.Object, scope.Object);

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void ResolveSearchScope_Concrete_Tenant_Request()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.TenantId).Returns((int?)null);
        var scope = new Mock<IPlatformScope>();
        scope.Setup(s => s.EnsureTenantAccess(7));

        var result = SearchAuditEventsQueryHandler.ResolveSearchScope(7, tenant.Object, scope.Object);

        result.AllCompanies.Should().BeFalse();
        result.TenantId.Should().Be(7);
        scope.Verify(s => s.EnsureTenantAccess(7), Times.Once);
    }

    [Theory]
    [InlineData("AdminVehicles", "ListByCompany", AuditEventKeys.CrossTenantRead)]
    [InlineData("AdminCompanies", "ListAllCompanies", AuditEventKeys.CrossTenantRead)]
    [InlineData("Subscription", "Upgrade", AuditEventKeys.SubscriptionChange)]
    [InlineData("User", "UpdateRoles", AuditEventKeys.RoleAssignment)]
    public void ResolveEventKey_Maps_Security_Categories(string entity, string action, string expected)
    {
        AuditEventRegistrySeed.ResolveEventKey(entity, action).Should().Be(expected);
    }

    [Fact]
    public void TargetTenantId_Semantics_Documented_In_Write_Record()
    {
        // Single-target: TenantId = subject, TargetTenantId = subject.
        var single = new AuditEventWrite(
            EventKey: AuditEventKeys.CrossTenantRead,
            TenantId: 7,
            TargetTenantId: 7,
            FailPolicy: AuditFailPolicy.FailLoud);
        single.TenantId.Should().Be(7);
        single.TargetTenantId.Should().Be(7);

        // Aggregate: platform tenant + NULL target.
        var aggregate = new AuditEventWrite(
            EventKey: AuditEventKeys.CrossTenantRead,
            TenantId: PlatformTenant.Id,
            TargetTenantId: null,
            FailPolicy: AuditFailPolicy.FailLoud);
        aggregate.TenantId.Should().Be(PlatformTenant.Id);
        aggregate.TargetTenantId.Should().BeNull();
    }

    [Fact]
    public void FailClosed_And_FailLoud_Categories_Are_Named()
    {
        // True fail-closed (same TX).
        AuditEventKeys.RolePermissions.Should().NotBeNullOrWhiteSpace();
        AuditEventKeys.RoleAssignment.Should().NotBeNullOrWhiteSpace();
        // Fail-loud at API boundary (not same TX) — SG-52.
        AuditEventKeys.CrossTenantRead.Should().NotBeNullOrWhiteSpace();
        AuditEventKeys.SubscriptionChange.Should().NotBeNullOrWhiteSpace();
        AuditEventKeys.PiiReveal.Should().NotBeNullOrWhiteSpace();
        AuditEventKeys.Impersonation.Should().NotBeNullOrWhiteSpace();
        AuditFailPolicy.FailLoud.Should().NotBe(AuditFailPolicy.FailClosed);
    }

    [Fact]
    public void AuditWriteMetrics_Increments_On_RecordFailure()
    {
        var before = AuditWriteMetrics.TotalFailures;
        AuditWriteMetrics.RecordFailure("test.event", "unit");
        AuditWriteMetrics.TotalFailures.Should().BeGreaterThan(before);
    }
}
