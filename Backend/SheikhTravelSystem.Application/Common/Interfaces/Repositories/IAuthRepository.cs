using SheikhTravelSystem.Application.Features.Users.DTOs;
using SheikhTravelSystem.Domain.Entities;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for authentication flows. SQL lives in Infrastructure.
/// </summary>
public interface IAuthRepository
{
    Task<AuthLoginUser?> FindActiveUserByEmailOrPhoneAsync(
        string emailOrPhone,
        CancellationToken cancellationToken = default);

    Task RecordFailedLoginAttemptAsync(
        int userId,
        int attempts,
        DateTime? lockoutEndUtc,
        CancellationToken cancellationToken = default);

    Task ClearFailedLoginAttemptsAsync(int userId, CancellationToken cancellationToken = default);

    Task<int?> GetDriverIdByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task UpdateRefreshTokenAsync(
        int userId,
        string refreshToken,
        DateTime expiry,
        CancellationToken cancellationToken = default);

    Task ClearRefreshTokenAsync(int userId, CancellationToken cancellationToken = default);

    Task<User?> FindUserByValidRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<AuthPasswordResetUser?> FindActiveUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task SetPasswordResetTokenAsync(
        int userId,
        string tokenHash,
        DateTime expiry,
        CancellationToken cancellationToken = default);

    Task<AuthPasswordResetTarget?> FindUserByValidPasswordResetTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        int userId,
        string passwordHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="Exceptions.NotFoundException"/> when the user is missing.
    /// </summary>
    Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed record AuthLoginUser(
    int Id,
    int TenantId,
    string FullName,
    string Email,
    string PasswordHash,
    string Phone,
    UserRole Role,
    bool IsActive,
    string? RefreshToken,
    DateTime? RefreshTokenExpiryTime,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool IsDeleted,
    int FailedLoginAttempts,
    DateTime? LockoutEndUtc,
    DateTime? PasswordChangedAt);

public sealed record AuthPasswordResetUser(int Id, int TenantId, string Email, string FullName);

public sealed record AuthPasswordResetTarget(int Id, int TenantId);
