using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace SheikhTravelSystem.API.Authentication;

/// <summary>
/// HttpOnly session cookies for browser clients (SG-54). The ERP opts in with
/// <c>useCookies: true</c> on login/refresh; its tokens then never reach JavaScript, so an XSS
/// cannot read them. Mobile clients keep sending <c>Authorization: Bearer</c> and are unaffected.
/// </summary>
public static class AuthCookies
{
    public const string AccessCookie = "sg_at";
    public const string RefreshCookie = "sg_rt";

    /// <summary>The refresh cookie is only ever sent to the auth endpoints.</summary>
    public const string RefreshPath = "/api/auth";

    /// <summary>
    /// A cross-site form cannot set a custom header, and CORS only lets allow-listed origins send
    /// one. Requiring it on cookie-authenticated writes is the CSRF guard (with SameSite=Strict).
    /// </summary>
    public const string CsrfHeader = "X-Requested-With";

    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    public static CookieOptions AccessOptions(bool secure, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expires,
        IsEssential = true
    };

    public static CookieOptions RefreshOptions(bool secure, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = RefreshPath,
        Expires = expires,
        IsEssential = true
    };

    /// <summary>
    /// Both cookies live as long as the refresh token. The access JWT inside <c>sg_at</c> still
    /// expires on its own; the API then answers 401 and the client refreshes.
    /// </summary>
    public static void Write(
        HttpResponse response, string accessToken, string refreshToken, bool secure, DateTimeOffset expires)
    {
        response.Cookies.Append(AccessCookie, accessToken, AccessOptions(secure, expires));
        response.Cookies.Append(RefreshCookie, refreshToken, RefreshOptions(secure, expires));
    }

    public static void Clear(HttpResponse response, bool secure)
    {
        response.Cookies.Delete(AccessCookie, AccessOptions(secure, DateTimeOffset.UnixEpoch));
        response.Cookies.Delete(RefreshCookie, RefreshOptions(secure, DateTimeOffset.UnixEpoch));
    }

    public static bool HasSessionCookie(HttpRequest request) =>
        request.Cookies.ContainsKey(AccessCookie) || request.Cookies.ContainsKey(RefreshCookie);

    /// <summary>The access token from the cookie, only when the caller sent no Authorization header.</summary>
    public static string? AccessTokenFromCookie(HttpRequest request)
    {
        if (request.Headers.ContainsKey("Authorization")) return null;
        return request.Cookies.TryGetValue(AccessCookie, out var token) && !string.IsNullOrWhiteSpace(token)
            ? token
            : null;
    }

    /// <summary>
    /// JwtBearer <c>OnMessageReceived</c> hook: use the cookie token when nothing else supplied one
    /// (no Authorization header, no SignalR query token).
    /// </summary>
    public static void ApplyCookieToken(MessageReceivedContext context)
    {
        if (string.IsNullOrEmpty(context.Token) && AccessTokenFromCookie(context.Request) is { } token)
            context.Token = token;
    }

    /// <summary>A token in the body wins (mobile); otherwise the refresh cookie (browser).</summary>
    public static (string? Token, bool FromCookie) ResolveRefreshToken(HttpRequest request, string? bodyToken)
    {
        if (!string.IsNullOrWhiteSpace(bodyToken)) return (bodyToken, false);
        return request.Cookies.TryGetValue(RefreshCookie, out var token) && !string.IsNullOrWhiteSpace(token)
            ? (token, true)
            : (null, false);
    }

    public static bool HasCsrfHeader(HttpRequest request) => request.Headers.ContainsKey(CsrfHeader);

    /// <summary>
    /// False only for a state-changing request that would be authenticated by the access cookie
    /// and lacks the CSRF header. Bearer requests, anonymous requests and reads always pass.
    /// </summary>
    public static bool PassesCsrfGuard(HttpRequest request)
    {
        if (SafeMethods.Contains(request.Method, StringComparer.OrdinalIgnoreCase)) return true;
        if (AccessTokenFromCookie(request) is null) return true;
        return HasCsrfHeader(request);
    }
}

public static class AuthCookieApplicationBuilderExtensions
{
    /// <summary>
    /// CSRF guard for cookie sessions: a write authenticated by the session cookie must carry
    /// <see cref="AuthCookies.CsrfHeader"/>. Bearer and anonymous requests (mobile, webhooks,
    /// device ingest) pass through untouched. Register after CORS so the refusal is readable
    /// by the ERP, and before authentication.
    /// </summary>
    public static IApplicationBuilder UseAuthCookieCsrfGuard(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!AuthCookies.PassesCsrfGuard(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Missing request header.",
                    code = "CSRF_HEADER_REQUIRED"
                });
                return;
            }

            await next();
        });
}
