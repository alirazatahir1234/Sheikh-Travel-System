namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Role { get; }
    int? DriverId { get; }
    /// <summary>Auth session id from JWT <c>sid</c> claim when present.</summary>
    Guid? SessionId { get; }
    bool HasPermission(string permission);
    bool IsPlatformSuperAdmin { get; }
    IReadOnlyList<string> Roles { get; }
}
