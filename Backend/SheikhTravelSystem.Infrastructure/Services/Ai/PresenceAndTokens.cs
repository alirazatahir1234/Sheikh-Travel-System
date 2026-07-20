using Dapper;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;

namespace SheikhTravelSystem.Infrastructure.Services.Ai;

public sealed class UserPresenceService(IDbConnectionFactory dbFactory) : IUserPresenceService
{
    public Task SetBrowserOnlineAsync(int userId, bool online, CancellationToken cancellationToken = default) =>
        UpsertAsync(userId, browserOnline: online, touchBrowser: true, cancellationToken: cancellationToken);

    public Task SetMobileHeartbeatAsync(int userId, CancellationToken cancellationToken = default) =>
        UpsertAsync(userId, mobileOnline: true, touchMobile: true, cancellationToken: cancellationToken);

    public Task MarkLoginAsync(int userId, CancellationToken cancellationToken = default) =>
        UpsertAsync(userId, markLogin: true, cancellationToken: cancellationToken);

    public Task MarkReadAsync(int userId, CancellationToken cancellationToken = default) =>
        UpsertAsync(userId, markRead: true, cancellationToken: cancellationToken);

    public async Task<UserPresenceSnapshot?> GetAsync(int userId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<UserPresenceSnapshot>(new CommandDefinition("""
            SELECT UserId, BrowserOnline, MobileOnline, LastBrowserAt, LastMobileAt, LastLoginAt, LastReadAt
            FROM UserPresence WHERE UserId = @UserId
            """, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<string>> SelectChannelsAsync(
        int? userId,
        int priority,
        IReadOnlyList<string> candidateChannels,
        CancellationToken cancellationToken = default)
    {
        if (userId is null)
            return candidateChannels;

        var presence = await GetAsync(userId.Value, cancellationToken);
        var selected = new List<string>();

        var browserActive = presence?.BrowserOnline == true
            && presence.LastBrowserAt is DateTime b
            && b > DateTime.UtcNow.AddMinutes(-5);

        var mobileActive = presence?.MobileOnline == true
            && presence.LastMobileAt is DateTime m
            && m > DateTime.UtcNow.AddMinutes(-15);

        foreach (var channel in candidateChannels)
        {
            if (channel is NotificationChannels.InApp)
            {
                selected.Add(channel);
                continue;
            }

            if (channel is NotificationChannels.Browser && browserActive)
            {
                selected.Add(channel);
                continue;
            }

            if (channel is NotificationChannels.Push && (mobileActive || !browserActive))
            {
                selected.Add(channel);
                continue;
            }

            if (channel is NotificationChannels.Email && !browserActive && !mobileActive)
            {
                selected.Add(channel);
                continue;
            }

            if (channel is NotificationChannels.Sms && priority >= 4)
            {
                selected.Add(channel);
            }
        }

        // Critical: always keep InApp + at least one interrupt channel
        if (priority >= 4)
        {
            if (!selected.Contains(NotificationChannels.InApp))
                selected.Insert(0, NotificationChannels.InApp);
            if (!selected.Any(c => c is NotificationChannels.Push or NotificationChannels.Sms or NotificationChannels.Browser))
                selected.Add(NotificationChannels.Push);
        }

        return selected.Count > 0 ? selected : candidateChannels;
    }

    private async Task UpsertAsync(
        int userId,
        bool? browserOnline = null,
        bool touchBrowser = false,
        bool? mobileOnline = null,
        bool touchMobile = false,
        bool markLogin = false,
        bool markRead = false,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE UserPresence AS t
            USING (SELECT @UserId AS UserId) AS s ON t.UserId = s.UserId
            WHEN MATCHED THEN UPDATE SET
                BrowserOnline = CASE WHEN @TouchBrowser = 1 THEN @BrowserOnline ELSE t.BrowserOnline END,
                MobileOnline = CASE WHEN @TouchMobile = 1 THEN @MobileOnline ELSE t.MobileOnline END,
                LastBrowserAt = CASE WHEN @TouchBrowser = 1 THEN GETUTCDATE() ELSE t.LastBrowserAt END,
                LastMobileAt = CASE WHEN @TouchMobile = 1 THEN GETUTCDATE() ELSE t.LastMobileAt END,
                LastLoginAt = CASE WHEN @MarkLogin = 1 THEN GETUTCDATE() ELSE t.LastLoginAt END,
                LastReadAt = CASE WHEN @MarkRead = 1 THEN GETUTCDATE() ELSE t.LastReadAt END,
                UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT
                (UserId, BrowserOnline, MobileOnline, LastBrowserAt, LastMobileAt, LastLoginAt, LastReadAt, UpdatedAt)
            VALUES
                (@UserId,
                 ISNULL(@BrowserOnline, 0),
                 ISNULL(@MobileOnline, 0),
                 CASE WHEN @TouchBrowser = 1 THEN GETUTCDATE() ELSE NULL END,
                 CASE WHEN @TouchMobile = 1 THEN GETUTCDATE() ELSE NULL END,
                 CASE WHEN @MarkLogin = 1 THEN GETUTCDATE() ELSE NULL END,
                 CASE WHEN @MarkRead = 1 THEN GETUTCDATE() ELSE NULL END,
                 GETUTCDATE());
            """,
            new
            {
                UserId = userId,
                BrowserOnline = browserOnline ?? false,
                TouchBrowser = touchBrowser,
                MobileOnline = mobileOnline ?? false,
                TouchMobile = touchMobile,
                MarkLogin = markLogin,
                MarkRead = markRead
            },
            cancellationToken: cancellationToken));
    }
}

public sealed class DeviceTokenService(IDbConnectionFactory dbFactory) : IDeviceTokenService
{
    public async Task RegisterAsync(int userId, string token, string platform, string appName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE UserDeviceTokens AS t
            USING (SELECT @Token AS Token) AS s ON t.Token = s.Token
            WHEN MATCHED THEN UPDATE SET
                UserId = @UserId, Platform = @Platform, AppName = @AppName,
                LastSeenAt = GETUTCDATE(), IsActive = 1, UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN INSERT (UserId, Token, Platform, AppName, LastSeenAt, IsActive, CreatedAt)
            VALUES (@UserId, @Token, @Platform, @AppName, GETUTCDATE(), 1, GETUTCDATE());
            """,
            new { UserId = userId, Token = token.Trim(), Platform = platform, AppName = appName },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<string>> GetActiveTokensAsync(int userId, int? tenantId = null, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT Token FROM UserDeviceTokens
            WHERE UserId = @UserId AND IsActive = 1
              AND (@TenantId IS NULL OR EXISTS (
                    SELECT 1 FROM Users u
                    WHERE u.Id = @UserId AND u.IsDeleted = 0 AND u.TenantId = @TenantId))
            """, new { UserId = userId, TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToList();
    }
}

public sealed class AlertNotificationAudit(IDbConnectionFactory dbFactory) : IAlertNotificationAudit
{
    public async Task LogAsync(
        int alertEventId, string channel, string? recipient, string status, string? error = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO AlertNotificationLogs (AlertEventId, Channel, Recipient, Status, SentAt, Error, CreatedAt)
            VALUES (@AlertEventId, @Channel, @Recipient, @Status,
                    CASE WHEN @Status = 'Sent' THEN GETUTCDATE() ELSE NULL END, @Error, GETUTCDATE())
            """,
            new { AlertEventId = alertEventId, Channel = channel, Recipient = recipient, Status = status, Error = error },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> IsAlertTypeEnabledAsync(
        int userId, string alertType, string channel, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<AlertPrefRow>(
            new CommandDefinition("""
                SELECT InAppEnabled, EmailEnabled, PushEnabled, SmsEnabled
                FROM AlertSettings WHERE UserId = @UserId AND AlertType = @AlertType
                """,
                new { UserId = userId, AlertType = alertType },
                cancellationToken: cancellationToken));

        if (row is null) return true; // default allow

        return channel switch
        {
            NotificationChannels.InApp or NotificationChannels.Browser => row.InAppEnabled,
            NotificationChannels.Email => row.EmailEnabled,
            NotificationChannels.Push => row.PushEnabled,
            NotificationChannels.Sms => row.SmsEnabled,
            _ => true
        };
    }

    private sealed class AlertPrefRow
    {
        public bool InAppEnabled { get; init; }
        public bool EmailEnabled { get; init; }
        public bool PushEnabled { get; init; }
        public bool SmsEnabled { get; init; }
    }
}
