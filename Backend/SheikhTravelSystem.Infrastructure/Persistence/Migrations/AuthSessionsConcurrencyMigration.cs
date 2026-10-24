using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence.Migrations;

/// <summary>
/// AuthSessions table for tenant-wide concurrent login limit (cap from AuthSessionLimits; 0 = unlimited).
/// </summary>
public static class AuthSessionsConcurrencyMigration
{
    public static async Task ApplyAsync(
        IDbConnectionFactory dbFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF OBJECT_ID(N'dbo.AuthSessions', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.AuthSessions (
                        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuthSessions PRIMARY KEY,
                        TenantId INT NOT NULL,
                        UserId INT NOT NULL,
                        RefreshTokenHash NVARCHAR(128) NOT NULL,
                        RefreshTokenExpiryUtc DATETIME2 NOT NULL,
                        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_AuthSessions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
                        LastSeenAtUtc DATETIME2 NULL,
                        RevokedAtUtc DATETIME2 NULL,
                        RevokeReason NVARCHAR(40) NULL,
                        ClientKind NVARCHAR(20) NULL,
                        CONSTRAINT FK_AuthSessions_Tenants FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
                        CONSTRAINT FK_AuthSessions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
                    );

                    CREATE UNIQUE INDEX UX_AuthSessions_RefreshHash_Active
                        ON dbo.AuthSessions (RefreshTokenHash)
                        WHERE RevokedAtUtc IS NULL;

                    CREATE INDEX IX_AuthSessions_Tenant_Active_Created
                        ON dbo.AuthSessions (TenantId, CreatedAtUtc)
                        WHERE RevokedAtUtc IS NULL;

                    CREATE INDEX IX_AuthSessions_UserId
                        ON dbo.AuthSessions (UserId);
                END
                """, cancellationToken: cancellationToken));

            logger.LogInformation("AuthSessionsConcurrencyMigration applied.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AuthSessionsConcurrencyMigration failed.");
            throw;
        }
    }
}
