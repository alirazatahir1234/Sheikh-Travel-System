namespace SheikhTravelSystem.API.Models;

/// <summary>
/// Login body. <c>UseCookies</c> is the browser opt-in (SG-54): tokens are set as HttpOnly
/// cookies and left out of the JSON. Omitted or false = tokens in the JSON, as mobile expects.
/// </summary>
public sealed record LoginRequest(string Email, string Password, bool UseCookies = false);

/// <summary>
/// Refresh body. Mobile sends <c>RefreshToken</c>. A browser in cookie mode sends
/// <c>UseCookies</c> and no token; the refresh token is read from its HttpOnly cookie.
/// </summary>
public sealed record RefreshTokenRequest(string? RefreshToken = null, bool UseCookies = false);
