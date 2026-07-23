using Dapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Auth.Commands;

/// <summary>
/// Authenticates users and issues access/refresh tokens.
/// Soft-enforces Stage 13 security policies (lockout, password age, IP allowlist).
/// </summary>
public class LoginCommandHandler(
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IUserAccessService userAccessService,
    IUserPresenceService presence,
    ISecurityEngine securityEngine,
    IConfiguration configuration,
    ILogger<LoginCommandHandler> logger) : IRequestHandler<LoginCommand, ApiResponse<LoginResponse>>
{
    public async Task<ApiResponse<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var user = await connection.QuerySingleOrDefaultAsync<LoginUserRow>(
            new CommandDefinition(
                @"SELECT Id, TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive,
                  RefreshToken, RefreshTokenExpiryTime, CreatedAt, UpdatedAt, IsDeleted,
                  FailedLoginAttempts, LockoutEndUtc, PasswordChangedAt
                  FROM Users WHERE (Email = @Email OR Phone = @Email) AND IsDeleted = 0 AND IsActive = 1",
                new { request.Email },
                cancellationToken: cancellationToken));

        if (user is null)
        {
            logger.LogWarning("Failed login attempt for email {Email}", request.Email);
            return ApiResponse<LoginResponse>.FailResponse("Invalid email or password.");
        }

        IReadOnlyDictionary<string, string> policies;
        try
        {
            policies = await securityEngine.GetEffectiveMapAsync(user.TenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Security engine unavailable during login for tenant {TenantId}", user.TenantId);
            policies = new Dictionary<string, string>();
        }

        if (user.LockoutEndUtc is DateTime lockoutEnd && lockoutEnd > DateTime.UtcNow)
        {
            return ApiResponse<LoginResponse>.FailResponse(
                $"Account locked. Try again after {lockoutEnd:u}.");
        }

        var clientIp = request.ClientIp;
        var restrictIp = securityEngine.GetBool(policies, SecurityPolicyKeys.IpRestrictEnabled, false);
        var cidrs = securityEngine.GetString(policies, SecurityPolicyKeys.IpAllowedCidrs, "");
        if (!securityEngine.IsClientIpAllowed(clientIp, restrictIp, cidrs))
        {
            logger.LogWarning("Login blocked by IP policy for {Email} from {Ip}", request.Email, clientIp);
            return ApiResponse<LoginResponse>.FailResponse("Login not allowed from this network.");
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for email {Email}", request.Email);
            await RecordFailedAttemptAsync(connection, user, policies, securityEngine, cancellationToken);
            return ApiResponse<LoginResponse>.FailResponse("Invalid email or password.");
        }

        var maxAge = securityEngine.GetInt(policies, SecurityPolicyKeys.PasswordMaxAgeDays, 0);
        var passwordChangedAt = user.PasswordChangedAt ?? user.CreatedAt;
        if (securityEngine.IsPasswordExpired(passwordChangedAt, maxAge))
        {
            return ApiResponse<LoginResponse>.FailResponse(
                "Password expired. Please reset your password or contact an administrator.");
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users SET FailedLoginAttempts = 0, LockoutEndUtc = NULL WHERE Id = @Id
            """, new { user.Id }, cancellationToken: cancellationToken));

        int? driverId = null;
        if (user.Role == UserRole.Driver)
        {
            driverId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT Id FROM Drivers WHERE UserId = @UserId AND IsDeleted = 0",
                new { UserId = user.Id },
                cancellationToken: cancellationToken));
        }

        var access = await userAccessService.ResolveAsync(user.Id, user.TenantId, cancellationToken);
        var domainUser = new Domain.Entities.User
        {
            Id = user.Id,
            TenantId = user.TenantId,
            FullName = user.FullName,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            Phone = user.Phone,
            Role = user.Role,
            IsActive = user.IsActive,
            RefreshToken = user.RefreshToken,
            RefreshTokenExpiryTime = user.RefreshTokenExpiryTime,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsDeleted = user.IsDeleted
        };
        // token service reads TenantId from BaseEntity
        var accessToken = jwtTokenService.GenerateAccessToken(domainUser, driverId, access);
        var refreshToken = jwtTokenService.GenerateRefreshToken();
        var expiryDays = int.TryParse(configuration["JwtSettings:RefreshTokenExpiryDays"], out var days) ? days : 7;

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET RefreshToken = @RefreshToken, RefreshTokenExpiryTime = @Expiry WHERE Id = @Id",
                new { RefreshToken = refreshToken, Expiry = DateTime.UtcNow.AddDays(expiryDays), user.Id },
                cancellationToken: cancellationToken));

        logger.LogInformation("User {Email} logged in successfully", request.Email);
        try
        {
            await presence.MarkLoginAsync(user.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record login presence for user {UserId}", user.Id);
        }

        var primaryRole = access.RoleCodes.FirstOrDefault() ?? user.Role.ToString();
        var response = new LoginResponse(
            accessToken,
            refreshToken,
            user.FullName,
            primaryRole,
            user.Email,
            user.Phone,
            user.TenantId,
            user.Id,
            access.RoleCodes,
            access.Permissions);
        return ApiResponse<LoginResponse>.SuccessResponse(response, "Login successful.");
    }

    private static async Task RecordFailedAttemptAsync(
        System.Data.IDbConnection connection,
        LoginUserRow user,
        IReadOnlyDictionary<string, string> policies,
        ISecurityEngine securityEngine,
        CancellationToken cancellationToken)
    {
        var maxAttempts = securityEngine.GetInt(policies, SecurityPolicyKeys.LockoutMaxAttempts, 0);
        if (maxAttempts <= 0) return;

        var attempts = user.FailedLoginAttempts + 1;
        DateTime? lockoutEnd = null;
        if (attempts >= maxAttempts)
        {
            var minutes = securityEngine.GetInt(policies, SecurityPolicyKeys.LockoutMinutes, 15);
            lockoutEnd = DateTime.UtcNow.AddMinutes(Math.Max(1, minutes));
            attempts = 0;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users SET FailedLoginAttempts = @Attempts, LockoutEndUtc = @LockoutEndUtc WHERE Id = @Id
            """,
            new { Attempts = attempts, LockoutEndUtc = lockoutEnd, user.Id },
            cancellationToken: cancellationToken));
    }

    private sealed class LoginUserRow
    {
        public int Id { get; init; }
        public int TenantId { get; init; }
        public string FullName { get; init; } = "";
        public string Email { get; init; } = "";
        public string PasswordHash { get; init; } = "";
        public string Phone { get; init; } = "";
        public UserRole Role { get; init; }
        public bool IsActive { get; init; }
        public string? RefreshToken { get; init; }
        public DateTime? RefreshTokenExpiryTime { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public bool IsDeleted { get; init; }
        public int FailedLoginAttempts { get; init; }
        public DateTime? LockoutEndUtc { get; init; }
        public DateTime? PasswordChangedAt { get; init; }
    }
}
