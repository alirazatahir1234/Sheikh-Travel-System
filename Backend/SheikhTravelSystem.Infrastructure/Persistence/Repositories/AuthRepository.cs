using Dapper;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Users.DTOs;
using SheikhTravelSystem.Domain.Entities;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class AuthRepository(IDbConnectionFactory dbFactory) : IAuthRepository
{
    public async Task<AuthLoginUser?> FindActiveUserByEmailOrPhoneAsync(
        string emailOrPhone,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuthLoginUser>(
            new CommandDefinition(
                @"SELECT Id, TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive,
                  RefreshToken, RefreshTokenExpiryTime, CreatedAt, UpdatedAt, IsDeleted,
                  FailedLoginAttempts, LockoutEndUtc, PasswordChangedAt
                  FROM Users WHERE (Email = @Email OR Phone = @Email) AND IsDeleted = 0 AND IsActive = 1",
                new { Email = emailOrPhone },
                cancellationToken: cancellationToken));
    }

    public async Task RecordFailedLoginAttemptAsync(
        int userId,
        int attempts,
        DateTime? lockoutEndUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users SET FailedLoginAttempts = @Attempts, LockoutEndUtc = @LockoutEndUtc WHERE Id = @Id
            """,
            new { Attempts = attempts, LockoutEndUtc = lockoutEndUtc, Id = userId },
            cancellationToken: cancellationToken));
    }

    public async Task ClearFailedLoginAttemptsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Users SET FailedLoginAttempts = 0, LockoutEndUtc = NULL WHERE Id = @Id
            """, new { Id = userId }, cancellationToken: cancellationToken));
    }

    public async Task<int?> GetDriverIdByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT Id FROM Drivers WHERE UserId = @UserId AND IsDeleted = 0",
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateRefreshTokenAsync(
        int userId,
        string refreshToken,
        DateTime expiry,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET RefreshToken = @RefreshToken, RefreshTokenExpiryTime = @Expiry WHERE Id = @Id",
                new { RefreshToken = refreshToken, Expiry = expiry, Id = userId },
                cancellationToken: cancellationToken));
    }

    public async Task ClearRefreshTokenAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Users SET RefreshToken = NULL, RefreshTokenExpiryTime = NULL WHERE Id = @UserId",
                new { UserId = userId },
                cancellationToken: cancellationToken));
    }

    public async Task<User?> FindUserByValidRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                @"SELECT Id, TenantId, FullName, Email, PasswordHash, Phone, Role, IsActive,
                  RefreshToken, RefreshTokenExpiryTime, CreatedAt, UpdatedAt, IsDeleted
                  FROM Users WHERE RefreshToken = @RefreshToken AND RefreshTokenExpiryTime > @Now AND IsDeleted = 0",
                new { RefreshToken = refreshToken, Now = DateTime.UtcNow },
                cancellationToken: cancellationToken));
    }

    public async Task<AuthPasswordResetUser?> FindActiveUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuthPasswordResetUser>(
            new CommandDefinition(
                @"SELECT TOP 1 Id, TenantId, Email, FullName
                  FROM Users
                  WHERE Email = @Email AND IsDeleted = 0 AND IsActive = 1",
                new { Email = email },
                cancellationToken: cancellationToken));
    }

    public async Task SetPasswordResetTokenAsync(
        int userId,
        string tokenHash,
        DateTime expiry,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Users
              SET PasswordResetTokenHash = @TokenHash,
                  PasswordResetTokenExpiryUtc = @Expiry,
                  UpdatedAt = @UpdatedAt
              WHERE Id = @Id",
            new { TokenHash = tokenHash, Expiry = expiry, UpdatedAt = DateTime.UtcNow, Id = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<AuthPasswordResetTarget?> FindUserByValidPasswordResetTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuthPasswordResetTarget>(
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
    }

    public async Task ResetPasswordAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
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
            new { Hash = passwordHash, Now = DateTime.UtcNow, Id = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<UserDto> GetCurrentUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var user = await connection.QuerySingleOrDefaultAsync<UserDto>(
            new CommandDefinition(
                @"SELECT Id, FullName, Email, Phone, Role, IsActive, CreatedAt
                  FROM Users WHERE Id = @Id AND IsDeleted = 0",
                new { Id = userId },
                cancellationToken: cancellationToken));

        if (user is null)
            throw new NotFoundException("User", userId);

        return user;
    }
}
