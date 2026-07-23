using FluentAssertions;
using SheikhTravelSystem.Application.Features.Platform;

namespace SheikhTravelSystem.Tests.Platform;

public class PermissionCoverageClassifierTests
{
    [Fact]
    public void AllowAnonymous_is_Public()
    {
        PermissionCoverageClassifier.Classify(
            "AuthController", "POST", allowAnonymous: true, hasAuthorize: false,
            permissionPolicies: [], roles: []).Should().Be(PermissionCoverageStatuses.Public);
    }

    [Fact]
    public void DevController_is_Internal()
    {
        PermissionCoverageClassifier.Classify(
            "DevController", "GET", allowAnonymous: false, hasAuthorize: false,
            permissionPolicies: [], roles: []).Should().Be(PermissionCoverageStatuses.Internal);
    }

    [Fact]
    public void RequirePermission_is_Protected()
    {
        PermissionCoverageClassifier.Classify(
            "VehiclesController", "GET", allowAnonymous: false, hasAuthorize: true,
            permissionPolicies: ["Vehicle.View"], roles: [])
            .Should().Be(PermissionCoverageStatuses.Protected);
    }

    [Fact]
    public void View_only_on_write_is_PartiallyProtected()
    {
        PermissionCoverageClassifier.Classify(
            "TripsController", "POST", allowAnonymous: false, hasAuthorize: true,
            permissionPolicies: ["Trip.View"], roles: [])
            .Should().Be(PermissionCoverageStatuses.PartiallyProtected);
    }

    [Fact]
    public void Write_with_specific_permission_is_Protected()
    {
        PermissionCoverageClassifier.Classify(
            "TripsController", "POST", allowAnonymous: false, hasAuthorize: true,
            permissionPolicies: ["Trip.Create"], roles: [])
            .Should().Be(PermissionCoverageStatuses.Protected);
    }

    [Fact]
    public void Auth_only_allowlist_is_Protected()
    {
        PermissionCoverageClassifier.Classify(
            "DataScopeController", "GET", allowAnonymous: false, hasAuthorize: true,
            permissionPolicies: [], roles: [])
            .Should().Be(PermissionCoverageStatuses.Protected);
    }

    [Fact]
    public void Role_gated_controller_is_Protected()
    {
        PermissionCoverageClassifier.Classify(
            "DriverAppController", "GET", allowAnonymous: false, hasAuthorize: true,
            permissionPolicies: [], roles: ["Driver"])
            .Should().Be(PermissionCoverageStatuses.Protected);
    }

    [Fact]
    public void Authorize_only_business_controller_is_PartiallyProtected()
    {
        PermissionCoverageClassifier.Classify(
            "OpsController", "GET", allowAnonymous: false, hasAuthorize: true,
            permissionPolicies: [], roles: [])
            .Should().Be(PermissionCoverageStatuses.PartiallyProtected);
    }
}
