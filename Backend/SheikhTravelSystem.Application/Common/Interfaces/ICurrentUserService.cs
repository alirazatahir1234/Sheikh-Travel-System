namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Role { get; }
    int? DriverId { get; }
    bool HasPermission(string permission);
    bool IsPlatformSuperAdmin { get; }
    IReadOnlyList<string> Roles { get; }
}
