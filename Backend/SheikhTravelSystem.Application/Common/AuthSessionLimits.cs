namespace SheikhTravelSystem.Application.Common;

/// <summary>
/// Product concurrency for authenticated sessions per company (tenant).
/// When <see cref="MaxActiveSessionsPerTenant"/> is &gt; 0, a login that would exceed the
/// cap is blocked; existing sessions are not kicked or invalidated.
/// When the max is 0, concurrent login limiting is disabled (unlimited).
/// </summary>
public static class AuthSessionLimits
{
    /// <summary>
    /// Max active sessions per tenant. <c>0</c> = unlimited (limit check skipped).
    /// Temporarily disabled so local/dev and multi-device use are not blocked.
    /// </summary>
    public const int MaxActiveSessionsPerTenant = 0;

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
    /// Shown when a concurrent login is blocked (HTTP 200 + success:false).
    /// </summary>
    public const string LimitReachedMessage =
        "This company already has the maximum active logins. Log out from another device first, then try again.";

    public const string LimitReachedCode = "SESSION_LIMIT_REACHED";

    /// <summary>
    /// Legacy copy for sessions previously revoked by kick-oldest (no longer issued on login).
    /// </summary>
    public const string SessionSupersededMessage =
        "Your session was ended because another login started.";
}
