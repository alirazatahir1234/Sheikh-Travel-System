using Dapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

public record ForgotPasswordCommand(string Email) : IRequest<ApiResponse<object>>;

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
    }
}

public class ForgotPasswordCommandHandler(
    IDbConnectionFactory dbFactory,
    IEnumerable<INotificationChannelSender> channelSenders,
    IConfiguration configuration,
    ILogger<ForgotPasswordCommandHandler> logger)
    : IRequestHandler<ForgotPasswordCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // Always return the same message to avoid account enumeration.
        const string okMessage = "If an account exists for that email, a reset link has been sent.";

        using var connection = dbFactory.CreateConnection();
        var user = await connection.QuerySingleOrDefaultAsync<(int Id, int TenantId, string Email, string FullName)>(
            new CommandDefinition(
                @"SELECT TOP 1 Id, TenantId, Email, FullName
                  FROM Users
                  WHERE Email = @Email AND IsDeleted = 0 AND IsActive = 1",
                new { request.Email },
                cancellationToken: cancellationToken));

        if (user == default)
            return ApiResponse<object>.SuccessResponse(new { }, okMessage);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Sha256(token);
        var expiry = DateTime.UtcNow.AddHours(2);

        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Users
              SET PasswordResetTokenHash = @TokenHash,
                  PasswordResetTokenExpiryUtc = @Expiry,
                  UpdatedAt = @UpdatedAt
              WHERE Id = @Id",
            new { TokenHash = tokenHash, Expiry = expiry, UpdatedAt = DateTime.UtcNow, user.Id },
            cancellationToken: cancellationToken));

        var portal = configuration["Notifications:Email:PortalUrl"]?.TrimEnd('/')
            ?? "https://app.sheikhgo.com";
        var resetUrl = $"{portal}/auth/reset-password?token={Uri.EscapeDataString(token)}";

        var sender = channelSenders.FirstOrDefault(s =>
            string.Equals(s.Channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase));
        if (sender is null)
        {
            logger.LogWarning("Password reset requested but email channel is unavailable for user {UserId}", user.Id);
            return ApiResponse<object>.SuccessResponse(new { }, okMessage);
        }

        var subject = "Reset your SheikhGo password";
        var html = $"""
            <!DOCTYPE html><html><body style="font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;padding:24px;">
            <div style="max-width:640px;margin:0 auto;background:#fff;border-radius:10px;overflow:hidden;">
              <div style="background:#0F766E;color:#fff;padding:20px;font-weight:700;">{WebUtility.HtmlEncode(subject)}</div>
              <div style="padding:20px;color:#334155;">
                <p>Hi {WebUtility.HtmlEncode(user.FullName)},</p>
                <p>We received a request to reset your SheikhGo password. This link expires in 2 hours.</p>
                <p style="margin:24px 0;"><a href="{WebUtility.HtmlEncode(resetUrl)}"
                   style="background:#0F766E;color:#fff;padding:12px 18px;border-radius:8px;text-decoration:none;font-weight:700;">Reset password</a></p>
                <p style="font-size:13px;color:#64748b;">If you did not request this, you can ignore this email.</p>
              </div>
            </div></body></html>
            """;

        var result = await sender.SendAsync(
            new ChannelSendRequest(0, user.Id, user.TenantId, subject, html, NotificationChannels.Email, Email: user.Email),
            cancellationToken);

        if (!result.Success)
            logger.LogWarning("Password reset email failed for user {UserId}: {Detail}", user.Id, result.Response);

        return ApiResponse<object>.SuccessResponse(new { }, okMessage);
    }

    internal static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}

public record ResetPasswordWithTokenCommand(string Token, string NewPassword) : IRequest<ApiResponse<object>>;

public class ResetPasswordWithTokenCommandValidator : AbstractValidator<ResetPasswordWithTokenCommand>
{
    public ResetPasswordWithTokenCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(6).WithMessage("Password must be at least 6 characters.");
    }
}

public class ResetPasswordWithTokenCommandHandler(
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher,
    ISecurityEngine securityEngine,
    ILogger<ResetPasswordWithTokenCommandHandler> logger)
    : IRequestHandler<ResetPasswordWithTokenCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(ResetPasswordWithTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = ForgotPasswordCommandHandler.Sha256(request.Token);
        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<(int Id, int TenantId)>(
            new CommandDefinition(
                @"SELECT TOP 1 Id, TenantId
                  FROM Users
                  WHERE PasswordResetTokenHash = @TokenHash
                    AND PasswordResetTokenExpiryUtc IS NOT NULL
                    AND PasswordResetTokenExpiryUtc > @Now
                    AND IsDeleted = 0
                    AND IsActive = 1",
                new { TokenHash = tokenHash, Now = DateTime.UtcNow },
                cancellationToken: cancellationToken));

        if (user == default)
            return ApiResponse<object>.FailResponse("This reset link is invalid or has expired.");

        try
        {
            var map = await securityEngine.GetEffectiveMapAsync(user.TenantId, cancellationToken);
            var minLen = securityEngine.GetInt(map, SecurityPolicyKeys.PasswordMinLength, 6);
            if (request.NewPassword.Length < minLen)
                return ApiResponse<object>.FailResponse($"Password must be at least {minLen} characters.");

            if (securityEngine.GetBool(map, SecurityPolicyKeys.PasswordComplexity, false))
            {
                var ok = request.NewPassword.Any(char.IsUpper)
                    && request.NewPassword.Any(char.IsLower)
                    && request.NewPassword.Any(char.IsDigit)
                    && request.NewPassword.Any(c => !char.IsLetterOrDigit(c));
                if (!ok)
                    return ApiResponse<object>.FailResponse("Password must include upper, lower, digit, and symbol characters.");
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Password policy lookup failed during reset; using FluentValidation floor.");
        }

        var hash = passwordHasher.Hash(request.NewPassword);
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Users
              SET PasswordHash = @Hash,
                  PasswordResetTokenHash = NULL,
                  PasswordResetTokenExpiryUtc = NULL,
                  PasswordChangedAt = @Now,
                  FailedLoginAttempts = 0,
                  LockoutEndUtc = NULL,
                  UpdatedAt = @Now
              WHERE Id = @Id",
            new { Hash = hash, Now = DateTime.UtcNow, user.Id },
            cancellationToken: cancellationToken));

        return ApiResponse<object>.SuccessResponse(new { }, "Password updated. You can sign in with your new password.");
    }
}
