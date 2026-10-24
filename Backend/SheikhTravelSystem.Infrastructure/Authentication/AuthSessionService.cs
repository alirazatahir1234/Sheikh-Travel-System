using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Authentication;

/// <summary>
/// SQL-backed auth sessions with tenant-wide max-2 concurrency (block 3rd login).
/// </summary>
public sealed class AuthSessionService(
    IDbConnectionFactory dbFactory,
    ILogger<AuthSessionService> logger) : IAuthSessionService
{
    public async Task<AuthSessionCreateResult> CreateSessionAsync(
        int tenantId,
        int userId,
        string refreshToken,
        DateTime refreshExpiryUtc,
        string clientKind,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        using var tx = connection.BeginTransaction();

        var activeCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1)
            FROM dbo.AuthSessions WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId
              AND RevokedAtUtc IS NULL
              AND RefreshTokenExpiryUtc > SYSUTCDATETIME()
            """,
            new { TenantId = tenantId },
            transaction: tx,
            cancellationToken: cancellationToken));

        if (activeCount >= AuthSessionLimits.MaxActiveSessionsPerTenant)
        {
            tx.Rollback();
            logger.LogInformation(
                "Blocked login for tenant {TenantId}: already {ActiveCount} active sessions (limit {Limit})",
                tenantId, activeCount, AuthSessionLimits.MaxActiveSessionsPerTenant);
            return AuthSessionCreateResult.Blocked();
        }

        var sessionId = Guid.NewGuid();
        var hash = HashRefreshToken(refreshToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO dbo.AuthSessions
                (Id, TenantId, UserId, RefreshTokenHash, RefreshTokenExpiryUtc, CreatedAtUtc, ClientKind)
            VALUES
                (@Id, @TenantId, @UserId, @Hash, @Expiry, SYSUTCDATETIME(), @ClientKind)
            """,
            new
            {
                Id = sessionId,
                TenantId = tenantId,
                UserId = userId,
                Hash = hash,
                Expiry = refreshExpiryUtc,
                ClientKind = Truncate(clientKind, 20)
            },
            transaction: tx,
            cancellationToken: cancellationToken));

        // Mirror onto Users for legacy single-token callers / driver refresh paths.
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.Users
            SET RefreshToken = @RefreshToken, RefreshTokenExpiryTime = @Expiry
            WHERE Id = @UserId
            """,
            new { RefreshToken = refreshToken, Expiry = refreshExpiryUtc, UserId = userId },
            transaction: tx,
            cancellationToken: cancellationToken));

        tx.Commit();
        return AuthSessionCreateResult.Created(sessionId);
    }

    public async Task<AuthSessionRecord?> FindActiveByRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        using var connection = dbFactory.CreateConnection();
        var hash = HashRefreshToken(refreshToken);
        var row = await connection.QuerySingleOrDefaultAsync<AuthSessionRecord>(
            new CommandDefinition("""
                SELECT Id, TenantId, UserId, RefreshTokenExpiryUtc, CreatedAtUtc, RevokedAtUtc, RevokeReason
                FROM dbo.AuthSessions
                WHERE RefreshTokenHash = @Hash
                  AND RevokedAtUtc IS NULL
                  AND RefreshTokenExpiryUtc > SYSUTCDATETIME()
                """,
                new { Hash = hash },
                cancellationToken: cancellationToken));

        return row;
    }

    public async Task RotateRefreshTokenAsync(
        Guid sessionId,
        int userId,
        string newRefreshToken,
        DateTime refreshExpiryUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
        using var tx = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.AuthSessions
            SET RefreshTokenHash = @Hash,
                RefreshTokenExpiryUtc = @Expiry,
                LastSeenAtUtc = SYSUTCDATETIME()
            WHERE Id = @Id AND RevokedAtUtc IS NULL
            """,
            new { Id = sessionId, Hash = HashRefreshToken(newRefreshToken), Expiry = refreshExpiryUtc },
            transaction: tx,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.Users
            SET RefreshToken = @RefreshToken, RefreshTokenExpiryTime = @Expiry
            WHERE Id = @UserId
            """,
            new { RefreshToken = newRefreshToken, Expiry = refreshExpiryUtc, UserId = userId },
            transaction: tx,
            cancellationToken: cancellationToken));

        tx.Commit();
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.AuthSessions
            SET RevokedAtUtc = SYSUTCDATETIME(), RevokeReason = @Reason
            WHERE Id = @Id AND RevokedAtUtc IS NULL
            """,
            new { Id = sessionId, Reason = Truncate(reason, 40) },
            cancellationToken: cancellationToken));
    }

    public async Task RevokeCurrentAsync(
        int userId,
        Guid? sessionId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
        using var tx = connection.BeginTransaction();

        if (sessionId is Guid sid)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE dbo.AuthSessions
                SET RevokedAtUtc = SYSUTCDATETIME(), RevokeReason = @Reason
                WHERE Id = @Id AND UserId = @UserId AND RevokedAtUtc IS NULL
                """,
                new { Id = sid, UserId = userId, Reason = AuthSessionLimits.RevokeReasonLogout },
                transaction: tx,
                cancellationToken: cancellationToken));
        }
        else
        {
            // Legacy tokens without sid: revoke all active sessions for this user.
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE dbo.AuthSessions
                SET RevokedAtUtc = SYSUTCDATETIME(), RevokeReason = @Reason
                WHERE UserId = @UserId AND RevokedAtUtc IS NULL
                """,
                new { UserId = userId, Reason = AuthSessionLimits.RevokeReasonLogout },
                transaction: tx,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.Users
            SET RefreshToken = NULL, RefreshTokenExpiryTime = NULL
            WHERE Id = @UserId
            """,
            new { UserId = userId },
            transaction: tx,
            cancellationToken: cancellationToken));

        tx.Commit();
    }

    public async Task<AuthSessionStatus> GetStatusAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(DateTime? RevokedAtUtc, string? RevokeReason, DateTime RefreshTokenExpiryUtc)?>(
            new CommandDefinition("""
                SELECT RevokedAtUtc, RevokeReason, RefreshTokenExpiryUtc
                FROM dbo.AuthSessions WHERE Id = @Id
                """,
                new { Id = sessionId },
                cancellationToken: cancellationToken));

        if (row is null)
            return AuthSessionStatus.Missing;

        var (revokedAt, reason, expiry) = row.Value;
        if (revokedAt is not null)
        {
            return string.Equals(reason, AuthSessionLimits.RevokeReasonSuperseded, StringComparison.OrdinalIgnoreCase)
                ? AuthSessionStatus.Superseded
                : AuthSessionStatus.Revoked;
        }

        if (expiry <= DateTime.UtcNow)
            return AuthSessionStatus.Expired;

        return AuthSessionStatus.Active;
    }

    public async Task TouchAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = dbFactory.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE dbo.AuthSessions
                SET LastSeenAtUtc = SYSUTCDATETIME()
                WHERE Id = @Id AND RevokedAtUtc IS NULL
                """,
                new { Id = sessionId },
                cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to touch auth session {SessionId}", sessionId);
        }
    }

    internal static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(bytes);
    }

    private static string? Truncate(string? value, int max)
        => string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);
}
