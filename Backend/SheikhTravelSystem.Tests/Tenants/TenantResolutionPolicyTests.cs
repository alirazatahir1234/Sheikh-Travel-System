using FluentAssertions;
using SheikhTravelSystem.Application.Common.Multitenancy;

namespace SheikhTravelSystem.Tests.Tenants;

public class TenantResolutionPolicyTests
{
    [Fact]
    public void Authenticated_MatchingHeader_AllowsJwtTenant()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: 1,
            headerTenantId: 1,
            slugResolvedTenantId: null,
            slug: null,
            isAuthenticated: true,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().Be(1);
        d.WarnReason.Should().BeNull();
    }

    [Fact]
    public void Authenticated_MismatchedHeader_Rejects()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: 1,
            headerTenantId: 2,
            slugResolvedTenantId: null,
            slug: null,
            isAuthenticated: true,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeTrue();
        d.TenantId.Should().BeNull();
        d.RejectReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Authenticated_ForeignSlug_JwtWins_WithWarning_NoReject()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: 1,
            headerTenantId: null,
            slugResolvedTenantId: 2,
            slug: "other-tenant",
            isAuthenticated: true,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().Be(1);
        d.WarnReason.Should().Contain("authoritative");
        d.WarnReason.Should().Contain("other-tenant");
    }

    [Fact]
    public void SuperAdmin_ForeignHeader_HeaderWins_WithWarning()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: 1,
            headerTenantId: 2,
            slugResolvedTenantId: null,
            slug: null,
            isAuthenticated: true,
            isPlatformOperator: true,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().Be(2);
        d.WarnReason.Should().Contain("Platform operator");
    }

    [Fact]
    public void Anonymous_ValidSlug_ResolvesTenant()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: null,
            headerTenantId: null,
            slugResolvedTenantId: 5,
            slug: "acme",
            isAuthenticated: false,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().Be(5);
        d.Slug.Should().Be("acme");
        d.WarnReason.Should().BeNull();
    }

    [Fact]
    public void Anonymous_InvalidSlug_DefaultAllowed_UsesTenantOne()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: null,
            headerTenantId: null,
            slugResolvedTenantId: null,
            slug: "missing",
            isAuthenticated: false,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().Be(TenantResolutionPolicy.AnonymousDefaultTenantId);
        d.WarnReason.Should().Contain("transitional default");
    }

    [Fact]
    public void Anonymous_InvalidSlug_DefaultDisabled_ReturnsNull()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: null,
            headerTenantId: null,
            slugResolvedTenantId: null,
            slug: "missing",
            isAuthenticated: false,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: false);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().BeNull();
        d.WarnReason.Should().Contain("no default");
    }

    [Fact]
    public void Anonymous_XTenantId_IsIgnored()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: null,
            headerTenantId: 99,
            slugResolvedTenantId: 3,
            slug: "portal",
            isAuthenticated: false,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().Be(3);
    }

    [Fact]
    public void Authenticated_NoHeader_UsesJwtTenant()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: 7,
            headerTenantId: null,
            slugResolvedTenantId: null,
            slug: null,
            isAuthenticated: true,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().Be(7);
        d.WarnReason.Should().BeNull();
    }

    [Fact]
    public void Authenticated_NoJwtTenant_PreservesUnset()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: null,
            headerTenantId: null,
            slugResolvedTenantId: null,
            slug: null,
            isAuthenticated: true,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeFalse();
        d.TenantId.Should().BeNull();
    }

    [Fact]
    public void Authenticated_NoJwt_WithHeader_RejectsForNormalUser()
    {
        var d = TenantResolutionPolicy.Decide(
            jwtTenantId: null,
            headerTenantId: 2,
            slugResolvedTenantId: null,
            slug: null,
            isAuthenticated: true,
            isPlatformOperator: false,
            allowAnonymousDefaultTenant: true);

        d.Reject.Should().BeTrue();
    }
}
