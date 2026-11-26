using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.API.Authentication;
using SheikhTravelSystem.API.Models;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Auth.Commands;
using SheikhTravelSystem.Application.Features.Auth.Queries;

namespace SheikhTravelSystem.API.Controllers;

[EnableRateLimiting("auth")]
/// <summary>
/// Handles authentication and token lifecycle endpoints.
/// </summary>
public class AuthController : BaseApiController
{
    /// <summary>
    /// Authenticates a user and returns access tokens.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await Mediator.Send(new LoginCommand(request.Email, request.Password, clientIp));
        return Ok(request.UseCookies ? MoveTokensToCookies(result) : result);
    }

    /// <summary>
    /// Refreshes access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest? request)
    {
        var (token, fromCookie) = AuthCookies.ResolveRefreshToken(Request, request?.RefreshToken);
        // The refresh cookie is ambient: require the custom header so another site cannot trigger it.
        if (fromCookie && !AuthCookies.HasCsrfHeader(Request))
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<LoginResponse>.FailResponse("Missing request header.", code: "CSRF_HEADER_REQUIRED"));

        var result = await Mediator.Send(new RefreshTokenCommand(token ?? string.Empty));
        var useCookies = fromCookie || request?.UseCookies == true;
        if (useCookies && !result.Success)
            AuthCookies.Clear(Response, UseSecureCookies());
        return Ok(useCookies ? MoveTokensToCookies(result) : result);
    }

    /// <summary>
    /// Invalidates the current authenticated session.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        var hadSessionCookie = AuthCookies.HasSessionCookie(Request);
        // Always clear the browser cookies, even when the access token inside has already expired.
        if (hadSessionCookie)
            AuthCookies.Clear(Response, UseSecureCookies());

        if (User.Identity?.IsAuthenticated == true)
            return Ok(await Mediator.Send(new LogoutCommand()));

        // Bearer clients keep the previous contract: no valid token → 401.
        return hadSessionCookie
            ? Ok(ApiResponse<bool>.SuccessResponse(true, "Signed out."))
            : Unauthorized();
    }

    /// <summary>
    /// Returns the authenticated user's profile (requires Bearer token).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
        => Ok(await Mediator.Send(new GetCurrentUserQuery()));

    /// <summary>Starts a password reset email flow (always returns a generic success message).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Completes password reset using a one-time token from email.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithTokenCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>
    /// Browser opt-in (SG-54): put the tokens in HttpOnly cookies and blank them in the JSON,
    /// so page scripts can never read them.
    /// </summary>
    private ApiResponse<LoginResponse> MoveTokensToCookies(ApiResponse<LoginResponse> result)
    {
        if (!result.Success || result.Data is not { } data || string.IsNullOrEmpty(data.AccessToken))
            return result;

        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var days = int.TryParse(configuration["JwtSettings:RefreshTokenExpiryDays"], out var d) ? d : 7;
        AuthCookies.Write(Response, data.AccessToken, data.RefreshToken, UseSecureCookies(),
            DateTimeOffset.UtcNow.AddDays(days));

        return new ApiResponse<LoginResponse>
        {
            Success = result.Success,
            Message = result.Message,
            Errors = result.Errors,
            Code = result.Code,
            Data = data with { AccessToken = string.Empty, RefreshToken = string.Empty }
        };
    }

    /// <summary>Secure everywhere except plain-HTTP local development (ng serve proxy on :4200).</summary>
    private bool UseSecureCookies()
        => Request.IsHttps
           || !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
}
