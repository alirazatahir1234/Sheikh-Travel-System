namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Tenant-wide concurrent authenticated session cap (max 2).
/// Enforcement lives in <see cref="Interfaces.IAuthSessionService"/> (AuthSessions table):
/// a third login is blocked; existing sessions stay active.
/// </summary>
public static class ConcurrentLoginLimits
{
    public const int MaxActiveSessionsPerTenant = AuthSessionLimits.MaxActiveSessionsPerTenant;

    public const string LimitReachedMessage = AuthSessionLimits.LimitReachedMessage;

    public const string LimitReachedCode = AuthSessionLimits.LimitReachedCode;

    /// <summary>Legacy alias — prefer <see cref="LimitReachedMessage"/>.</summary>
    public const string SessionSupersededMessage = AuthSessionLimits.SessionSupersededMessage;

    /// <summary>Legacy helper — prefer AuthSessions status checks.</summary>
    public static bool HasActiveRefreshSession(string? refreshToken, DateTime? refreshTokenExpiryUtc)
        => !string.IsNullOrWhiteSpace(refreshToken)
           && refreshTokenExpiryUtc is DateTime expiry
           && expiry > DateTime.UtcNow;
}
