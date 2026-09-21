namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Development-only data utilities. SQL lives in Infrastructure; API keeps env gating.
/// </summary>
public interface IDevDataService
{
    Task<int> ResetAdminPasswordAsync(string passwordHash, CancellationToken cancellationToken = default);

    Task<DevDriverLoginFixResult?> FixDriverLoginAsync(string passwordHash, CancellationToken cancellationToken = default);

    Task<int> MigrateBookingNumberAsync(CancellationToken cancellationToken = default);
}

public sealed record DevDriverLoginFixResult(
    int DriverId,
    string? DriverPhone,
    int? UserId,
    string? UserPhone);
