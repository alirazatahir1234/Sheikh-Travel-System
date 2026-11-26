using System.Security.Claims;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SheikhTravelSystem.API.Authentication;
using SheikhTravelSystem.API.Controllers;
using SheikhTravelSystem.API.Models;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Auth.Commands;

namespace SheikhTravelSystem.Tests.Auth;

/// <summary>
/// SG-54: browser sessions use HttpOnly cookies; the bearer contract used by the mobile apps
/// (tokens in the JSON body, Authorization header) must stay exactly as it was.
/// </summary>
public class AuthCookieSessionTests
{
    private static readonly LoginResponse Tokens =
        new("access-jwt", "refresh-token", "Test User", "Admin", "user@example.com");

    private static HttpRequest Request(string method = "POST", string? cookie = null, string? authorization = null,
        bool csrfHeader = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (cookie is not null) context.Request.Headers.Cookie = cookie;
        if (authorization is not null) context.Request.Headers.Authorization = authorization;
        if (csrfHeader) context.Request.Headers[AuthCookies.CsrfHeader] = "XMLHttpRequest";
        return context.Request;
    }

    // ── cookie flags ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cookies_AreHttpOnly_SameSiteStrict_AndSecure()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(7);

        foreach (var options in new[] { AuthCookies.AccessOptions(true, expires), AuthCookies.RefreshOptions(true, expires) })
        {
            options.HttpOnly.Should().BeTrue("page scripts must never read a session token");
            options.SameSite.Should().Be(SameSiteMode.Strict);
            options.Secure.Should().BeTrue();
        }
    }

    [Fact]
    public void RefreshCookie_IsScopedToTheAuthEndpoints()
    {
        var expires = DateTimeOffset.UtcNow.AddDays(7);

        AuthCookies.RefreshOptions(true, expires).Path.Should().Be("/api/auth");
        AuthCookies.AccessOptions(true, expires).Path.Should().Be("/");
    }

    // ── token source ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AccessToken_IsReadFromCookie_OnlyWithoutAnAuthorizationHeader()
    {
        AuthCookies.AccessTokenFromCookie(Request(cookie: "sg_at=cookie-jwt")).Should().Be("cookie-jwt");
        AuthCookies.AccessTokenFromCookie(Request(cookie: "sg_at=cookie-jwt", authorization: "Bearer header-jwt"))
            .Should().BeNull("an explicit bearer token (mobile) always wins");
        AuthCookies.AccessTokenFromCookie(Request()).Should().BeNull();
    }

    [Fact]
    public void RefreshToken_BodyWins_ThenCookie()
    {
        AuthCookies.ResolveRefreshToken(Request(cookie: "sg_rt=from-cookie"), "from-body")
            .Should().Be(("from-body", false));
        AuthCookies.ResolveRefreshToken(Request(cookie: "sg_rt=from-cookie"), null)
            .Should().Be(("from-cookie", true));
        AuthCookies.ResolveRefreshToken(Request(), "  ").Should().Be(((string?)null, false));
    }

    // ── CSRF guard ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void CsrfGuard_RefusesACookieAuthenticatedWrite_WithoutTheHeader(string method)
    {
        AuthCookies.PassesCsrfGuard(Request(method, cookie: "sg_at=jwt")).Should().BeFalse();
        AuthCookies.PassesCsrfGuard(Request(method, cookie: "sg_at=jwt", csrfHeader: true)).Should().BeTrue();
    }

    [Fact]
    public void CsrfGuard_NeverBlocksReads_BearerClients_OrAnonymousCallers()
    {
        AuthCookies.PassesCsrfGuard(Request("GET", cookie: "sg_at=jwt")).Should().BeTrue();
        AuthCookies.PassesCsrfGuard(Request("POST", authorization: "Bearer mobile-jwt")).Should().BeTrue();
        AuthCookies.PassesCsrfGuard(Request("POST", cookie: "sg_at=jwt", authorization: "Bearer mobile-jwt"))
            .Should().BeTrue();
        AuthCookies.PassesCsrfGuard(Request("POST")).Should().BeTrue("webhooks and device ingest carry no session cookie");
    }

    // ── controller ──────────────────────────────────────────────────────────────────────────

    private static (AuthController Controller, Mock<ISender> Sender, HttpContext Http) Controller(
        string? cookie = null, bool csrfHeader = false, bool authenticated = false)
    {
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<LoginResponse>.SuccessResponse(Tokens));
        sender.Setup(s => s.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<LoginResponse>.SuccessResponse(Tokens));
        sender.Setup(s => s.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.SuccessResponse(true));

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Production");

        var services = new ServiceCollection()
            .AddSingleton(sender.Object)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["JwtSettings:RefreshTokenExpiryDays"] = "7" })
                .Build())
            .AddSingleton(environment.Object)
            .BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Method = "POST";
        if (cookie is not null) http.Request.Headers.Cookie = cookie;
        if (csrfHeader) http.Request.Headers[AuthCookies.CsrfHeader] = "XMLHttpRequest";
        if (authenticated)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "1")], "Test"));

        var controller = new AuthController { ControllerContext = new ControllerContext { HttpContext = http } };
        return (controller, sender, http);
    }

    private static LoginResponse Body(IActionResult result) =>
        ((ApiResponse<LoginResponse>)((OkObjectResult)result).Value!).Data!;

    private static string SetCookie(HttpContext http) => string.Join("\n", http.Response.Headers.SetCookie.ToArray());

    [Fact]
    public async Task Login_Bearer_ReturnsTokensInJson_AndSetsNoCookie()
    {
        var (controller, _, http) = Controller();

        var body = Body(await controller.Login(new LoginRequest("user@example.com", "pw")));

        body.AccessToken.Should().Be("access-jwt");
        body.RefreshToken.Should().Be("refresh-token");
        http.Response.Headers.SetCookie.Count.Should().Be(0, "the mobile contract must not change");
    }

    [Fact]
    public async Task Login_Cookies_SetsHttpOnlyCookies_AndBlanksTokensInJson()
    {
        var (controller, _, http) = Controller();

        var body = Body(await controller.Login(new LoginRequest("user@example.com", "pw", UseCookies: true)));

        body.AccessToken.Should().BeEmpty();
        body.RefreshToken.Should().BeEmpty();
        body.FullName.Should().Be("Test User", "the profile still comes back");
        var cookies = SetCookie(http);
        cookies.Should().Contain("sg_at=access-jwt").And.Contain("sg_rt=refresh-token");
        cookies.ToLowerInvariant().Should().Contain("httponly").And.Contain("samesite=strict").And.Contain("secure");
    }

    [Fact]
    public async Task Login_Failure_SetsNoCookie()
    {
        var (controller, sender, http) = Controller();
        sender.Setup(s => s.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<LoginResponse>.FailResponse("Invalid credentials."));

        await controller.Login(new LoginRequest("user@example.com", "wrong", UseCookies: true));

        http.Response.Headers.SetCookie.Count.Should().Be(0);
    }

    [Fact]
    public async Task Refresh_Bearer_UsesTheBodyToken_AndReturnsTokensInJson()
    {
        var (controller, sender, http) = Controller();

        var body = Body(await controller.RefreshToken(new RefreshTokenRequest("body-token")));

        sender.Verify(s => s.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "body-token"),
            It.IsAny<CancellationToken>()), Times.Once);
        body.AccessToken.Should().Be("access-jwt");
        http.Response.Headers.SetCookie.Count.Should().Be(0);
    }

    [Fact]
    public async Task Refresh_Cookie_UsesTheCookieToken_AndRotatesTheCookies()
    {
        var (controller, sender, http) = Controller(cookie: "sg_rt=cookie-token", csrfHeader: true);

        var body = Body(await controller.RefreshToken(new RefreshTokenRequest(UseCookies: true)));

        sender.Verify(s => s.Send(It.Is<RefreshTokenCommand>(c => c.RefreshToken == "cookie-token"),
            It.IsAny<CancellationToken>()), Times.Once);
        body.AccessToken.Should().BeEmpty();
        SetCookie(http).Should().Contain("sg_at=access-jwt").And.Contain("sg_rt=refresh-token");
    }

    [Fact]
    public async Task Refresh_Cookie_WithoutTheCsrfHeader_IsRefused()
    {
        var (controller, sender, _) = Controller(cookie: "sg_rt=cookie-token");

        var result = await controller.RefreshToken(null);

        ((ObjectResult)result).StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(s => s.Send(It.IsAny<RefreshTokenCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_LegacyBodyToken_WithUseCookies_UpgradesTheSessionToCookies()
    {
        // An ERP tab that still holds localStorage tokens upgrades itself on first refresh.
        var (controller, _, http) = Controller();

        var body = Body(await controller.RefreshToken(new RefreshTokenRequest("legacy-token", UseCookies: true)));

        body.RefreshToken.Should().BeEmpty();
        SetCookie(http).Should().Contain("sg_rt=refresh-token");
    }

    [Fact]
    public async Task Logout_WithExpiredCookieSession_StillClearsTheCookies()
    {
        var (controller, sender, http) = Controller(cookie: "sg_at=expired; sg_rt=token", csrfHeader: true);

        var result = await controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
        SetCookie(http).Should().Contain("sg_at=;").And.Contain("sg_rt=;");
        sender.Verify(s => s.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Logout_Authenticated_RevokesTheSession()
    {
        var (controller, sender, _) = Controller(authenticated: true);

        (await controller.Logout()).Should().BeOfType<OkObjectResult>();

        sender.Verify(s => s.Send(It.IsAny<LogoutCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_Bearer_WithoutAValidToken_IsStill401()
    {
        var (controller, _, _) = Controller();

        (await controller.Logout()).Should().BeOfType<UnauthorizedResult>();
    }
}
