namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Persists authenticated sessions and enforces tenant-wide concurrency.
/// </summary>
public interface IAuthSessionService
{
    /// <summary>
    /// Creates a new active session. When
    /// <see cref="AuthSessionLimits.MaxActiveSessionsPerTenant"/> is &gt; 0 and the tenant
    /// is already at the cap, returns blocked without creating a session or revoking existing ones.
    /// When the max is 0, the concurrency check is skipped (unlimited).
    /// Also mirrors the refresh token onto <c>Users</c> for legacy callers when created.
    /// </summary>
    Task<AuthSessionCreateResult> CreateSessionAsync(
        int tenantId,
        int userId,
        string refreshToken,
        DateTime refreshExpiryUtc,
        string clientKind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up an active session by refresh token (hash). Returns null when missing/expired/revoked.
    /// </summary>
    Task<AuthSessionRecord?> FindActiveByRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the refresh token for an existing session (same session id).
    /// </summary>
    Task RotateRefreshTokenAsync(
        Guid sessionId,
        int userId,
        string newRefreshToken,
        DateTime refreshExpiryUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a single session (logout). Also clears <c>Users.RefreshToken</c> when it matches.
    /// </summary>
    Task RevokeSessionAsync(
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the caller's current session from JWT <c>sid</c>, or falls back to clearing user refresh token.
    /// </summary>
    Task RevokeCurrentAsync(
        int userId,
        Guid? sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether the session is still active (not revoked, refresh not expired).
    /// </summary>
    Task<AuthSessionStatus> GetStatusAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Touch last-seen for an active session (best-effort).
    /// </summary>
    Task TouchAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of attempting to create an auth session under the tenant concurrency cap.
/// </summary>
public sealed record AuthSessionCreateResult(
    Guid SessionId,
    bool LimitReached)
{
    public static AuthSessionCreateResult Created(Guid sessionId)
        => new(sessionId, LimitReached: false);

    public static AuthSessionCreateResult Blocked()
        => new(Guid.Empty, LimitReached: true);
}

public sealed record AuthSessionRecord(
    Guid Id,
    int TenantId,
    int UserId,
    DateTime RefreshTokenExpiryUtc,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc,
    string? RevokeReason);

public enum AuthSessionStatus
{
    Missing = 0,
    Active = 1,
    Revoked = 2,
    Superseded = 3,
    Expired = 4
}
