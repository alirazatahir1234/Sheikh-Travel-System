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

    private static string Jwt(DateTime? expires = null)
    {
        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: [new Claim("sub", "42")],
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

    [Fact]
    public async Task AnonymousWrite_IsNotBlockedByTheGuard()
    {
        // Webhooks and device ingest carry no session cookie: the guard lets them through to
        // their own authentication.
        (await Send("POST")).Status.Should().Be(401);
    }
}
