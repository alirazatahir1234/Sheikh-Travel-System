namespace SheikhTravelSystem.Application.Features.Platform;

public static class PermissionCoverageStatuses
{
    public const string Protected = "Protected";
    public const string PartiallyProtected = "PartiallyProtected";
    public const string Public = "Public";
    public const string Internal = "Internal";
}

public sealed record PermissionCoverageEndpointDto(
    string Module,
    string Controller,
    string Action,
    string HttpMethod,
    string Route,
    string? RequiredPermission,
    string CoverageStatus,
    string? Notes);

public sealed record PermissionCoverageReportDto(
    int TotalEndpoints,
    int ProtectedCount,
    int PartiallyProtectedCount,
    int PublicCount,
    int InternalCount,
    IReadOnlyList<PermissionCoverageEndpointDto> Endpoints);
