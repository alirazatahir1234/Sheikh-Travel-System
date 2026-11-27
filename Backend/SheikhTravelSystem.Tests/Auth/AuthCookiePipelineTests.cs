using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SheikhTravelSystem.API.Authentication;

namespace SheikhTravelSystem.Tests.Auth;

/// <summary>
/// SG-54: runs the real ASP.NET authentication middleware and JwtBearer handler with the same two
/// hooks Program.cs registers (<see cref="AuthCookies.ApplyCookieToken"/> and
/// <c>UseAuthCookieCsrfGuard</c>), to prove a cookie session authenticates and the bearer path
/// used by the mobile apps is untouched.
/// </summary>
public class AuthCookiePipelineTests
{
    private const string Secret = "unit-test-signing-key-0123456789-abcdefghij";
    private static readonly SymmetricSecurityKey Key = new(Encoding.UTF8.GetBytes(Secret));

    /// <param name="permissions">
    /// Permission claims to add, as JwtTokenService does for every permission the user holds.
    /// </param>
    private static string Jwt(DateTime? expires = null, int permissions = 0)
    {
        var claims = new List<Claim> { new("sub", "42") };
        for (var i = 0; i < permissions; i++)
            claims.Add(new Claim("permission", $"Module{i / 6:D2}.Permission{i % 6}Manage"));

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: expires ?? DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(Key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<(int Status, bool Authenticated)> Send(
        string method, string? cookie = null, string? bearer = null, bool csrfHeader = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = "test-issuer",
                    ValidAudience = "test-audience",
                    IssuerSigningKey = Key,
                    ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        AuthCookies.ApplyCookieToken(context);
                        return Task.CompletedTask;
                    }
                };
            });
        var provider = services.BuildServiceProvider();

        var authenticated = false;
        var app = new ApplicationBuilder(provider);
        app.UseAuthCookieCsrfGuard();
        app.UseAuthentication();
        app.Run(context =>
        {
            authenticated = context.User.Identity?.IsAuthenticated == true;
            context.Response.StatusCode = authenticated ? StatusCodes.Status200OK : StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        });
        var pipeline = app.Build();

        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Method = method;
        http.Request.Path = "/api/vehicles";
        http.Response.Body = new MemoryStream();
        if (cookie is not null) http.Request.Headers.Cookie = cookie;
        if (bearer is not null) http.Request.Headers.Authorization = $"Bearer {bearer}";
        if (csrfHeader) http.Request.Headers[AuthCookies.CsrfHeader] = "XMLHttpRequest";

        await pipeline(http);
        return (http.Response.StatusCode, authenticated);
    }

    [Fact]
    public async Task CookieSession_AuthenticatesARead()
    {
        var (status, authenticated) = await Send("GET", cookie: $"sg_at={Jwt()}");

        status.Should().Be(200);
        authenticated.Should().BeTrue();
    }

    [Fact]
    public async Task CookieSession_Write_NeedsTheCsrfHeader()
    {
        var cookie = $"sg_at={Jwt()}";

        (await Send("POST", cookie: cookie)).Status.Should().Be(403);
        (await Send("POST", cookie: cookie, csrfHeader: true)).Should().Be((200, true));
    }

    [Fact]
    public async Task ExpiredCookieToken_Is401_SoTheClientRefreshes()
    {
        var expired = Jwt(DateTime.UtcNow.AddMinutes(-1));

        (await Send("GET", cookie: $"sg_at={expired}")).Should().Be((401, false));
    }

    [Fact]
    public async Task BearerHeader_StillAuthenticates_WithNoCsrfHeader()
    {
        (await Send("POST", bearer: Jwt())).Should().Be((200, true));
    }

    [Fact]
    public async Task BearerHeader_WinsOverACookie()
    {
        // A valid cookie must not rescue an invalid explicit bearer token.
        var result = await Send("GET", cookie: $"sg_at={Jwt()}", bearer: "not-a-jwt");

        result.Should().Be((401, false));
    }

    // ── an administrator's token: larger than one cookie may be ────────────────────────────

    /// <summary>Browsers drop a cookie whose name and value exceed this.</summary>
    private const int BrowserCookieLimit = 4096;

    /// <summary>The cookies <see cref="AuthCookies.Write"/> sets, as (name, value, full Set-Cookie line).</summary>
    private static List<(string Name, string Value, string Line)> WrittenCookies(string accessToken)
    {
        var http = new DefaultHttpContext();
        AuthCookies.Write(http.Response, accessToken, "refresh-token", secure: false, DateTimeOffset.UtcNow.AddDays(7));
        return http.Response.Headers.SetCookie
            .Select(line => line!)
            .Select(line =>
            {
                var pair = line.Split(';')[0];
                var eq = pair.IndexOf('=');
                return (pair[..eq], pair[(eq + 1)..], line);
            })
            .ToList();
    }

    /// <summary>The Cookie header a browser sends to /api/... (the refresh cookie is scoped to /api/auth).</summary>
    private static string BrowserCookieHeader(IEnumerable<(string Name, string Value, string Line)> cookies) =>
        string.Join("; ", cookies.Where(c => c.Name != AuthCookies.RefreshCookie).Select(c => $"{c.Name}={c.Value}"));

    [Fact]
    public async Task LargeToken_IsSplitIntoCookiesUnderTheBrowserLimit_AndStillAuthenticates()
    {
        var token = Jwt(permissions: 400);
        token.Length.Should().BeGreaterThan(3 * BrowserCookieLimit, "an administrator's token holds every permission");

        var cookies = WrittenCookies(token);

        cookies.Should().OnlyContain(c => c.Name.Length + 1 + c.Value.Length <= BrowserCookieLimit,
            "a browser silently drops a larger cookie, which signed every administrator straight out");
        cookies.Select(c => c.Name).Should().Contain(["sg_at", "sg_atC1", "sg_atC2", "sg_atC3", "sg_rt"]);
        cookies.Where(c => c.Name.StartsWith("sg_at")).Should().OnlyContain(c =>
            c.Line.Contains("httponly") && c.Line.Contains("samesite=strict") && c.Line.Contains("path=/;"));

        (await Send("GET", cookie: BrowserCookieHeader(cookies))).Should().Be((200, true));
    }

    [Fact]
    public async Task SplitCookie_WithAPartMissing_Is401_SoTheClientRefreshes()
    {
        var cookies = WrittenCookies(Jwt(permissions: 400));
        var lastPart = cookies.Where(c => c.Name.StartsWith("sg_atC")).OrderBy(c => c.Name.Length).ThenBy(c => c.Name).Last();
        cookies.Remove(lastPart);

        (await Send("GET", cookie: BrowserCookieHeader(cookies))).Should().Be((401, false));
    }

    [Fact]
    public void SmallToken_IsStillOnePlainCookie()
    {
        var token = Jwt();

        var access = WrittenCookies(token).Where(c => c.Name.StartsWith("sg_at")).ToList();

        access.Should().ContainSingle().Which.Value.Should().Be(token);
    }

    [Fact]
    public void Clear_DeletesEveryPartOfASplitCookie()
    {
        var cookies = WrittenCookies(Jwt(permissions: 400));
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));

        AuthCookies.Clear(http.Response, secure: false);

        var deleted = http.Response.Headers.SetCookie.Select(line => line!.Split('=')[0]).ToList();
        deleted.Should().BeEquivalentTo(cookies.Select(c => c.Name),
            "signing out must not leave parts of the session behind");
        http.Response.Headers.SetCookie.Should().OnlyContain(line => line!.Contains("expires=Thu, 01 Jan 1970"));
    }

    [Fact]
    public async Task AnonymousWrite_IsNotBlockedByTheGuard()
    {
        // Webhooks and device ingest carry no session cookie: the guard lets them through to
        // their own authentication.
        (await Send("POST")).Status.Should().Be(401);
    }
}
