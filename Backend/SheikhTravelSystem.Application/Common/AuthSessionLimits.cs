namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Product concurrency: at most two active authenticated sessions per company (tenant).
/// A third login attempt is blocked; existing sessions are not kicked or invalidated.
/// </summary>
public static class AuthSessionLimits
{
    public const int MaxActiveSessionsPerTenant = 2;

    public const string ClaimSessionId = "sid";

    public const string RevokeReasonLogout = "logout";
    public const string RevokeReasonSuperseded = "superseded";
    public const string RevokeReasonRotated = "rotated";

    public const string ClientKindWeb = "web";
    public const string ClientKindDriver = "driver";
    public const string ClientKindMobile = "mobile";

    public const string SessionEndedHeader = "X-Session-Ended";
    public const string SessionEndedSuperseded = "superseded";

    /// <summary>
    /// Shown when a third concurrent login is attempted (HTTP 200 + success:false).
    /// </summary>
    public const string LimitReachedMessage =
        "This company already has 2 active logins. Log out from another device first, then try again.";

    public const string LimitReachedCode = "SESSION_LIMIT_REACHED";

    /// <summary>
    /// Legacy copy for sessions previously revoked by kick-oldest (no longer issued on login).
    /// </summary>
    public const string SessionSupersededMessage =
        "Your session was ended because another login started.";
}
