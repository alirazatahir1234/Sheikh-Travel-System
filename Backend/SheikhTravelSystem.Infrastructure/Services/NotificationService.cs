using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Services.Notifications;

namespace SheikhTravelSystem.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];

    private const int MaxRetries = 3;

    private readonly IDbConnectionFactory _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly IEnumerable<INotificationChannelSender> _senders;
    private readonly INotificationRealtimePublisher _realtime;
    private readonly IDistributedCache _cache;
    private readonly IAlertNotificationAudit _alertAudit;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IDbConnectionFactory dbFactory,
        ITenantContext tenantContext,
        IEnumerable<INotificationChannelSender> senders,
        INotificationRealtimePublisher realtime,
        IDistributedCache cache,
        IAlertNotificationAudit alertAudit,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _senders = senders;
        _realtime = realtime;
        _cache = cache;
        _alertAudit = alertAudit;
        _configuration = configuration;
        _logger = logger;
    }

    public Task CreateAsync(
        int? userId,
        string title,
        string message,
        NotificationType type,
        int? referenceId = null,
        CancellationToken cancellationToken = default) =>
        CreateAndDispatchAsync(new NotificationCreateOptions(
            userId, title, message, type, referenceId,
            Channel: NotificationChannels.InApp,
            Module: "System",
            SendNow: true), cancellationToken);

    public Task CreateForAllAsync(
        string title,
        string message,
        NotificationType type,
        int? referenceId = null,
        CancellationToken cancellationToken = default) =>
        CreateForAllChannelsAsync(
            title, message, type,
            [NotificationChannels.InApp],
            priority: 2,
            module: "System",
            referenceId: referenceId,
            cancellationToken: cancellationToken);

    public async Task CreateForAllChannelsAsync(
        string title,
        string message,
        NotificationType type,
        IReadOnlyList<string> channels,
        int priority = 2,
        string? module = null,
        int? referenceId = null,
        string? templateKey = null,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();
        var tenantId = _tenantContext.TenantId ?? 1;
        var userIds = (await connection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT Id FROM Users WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        var channelList = channels is { Count: > 0 } ? channels : [NotificationChannels.InApp];

        foreach (var userId in userIds)
        {
            foreach (var channel in channelList)
            {
                await CreateAndDispatchAsync(new NotificationCreateOptions(
                    userId, title, message, type, referenceId, priority,
                    Channel: channel,
                    TemplateKey: templateKey,
                    Module: module,
                    Variables: variables,
                    SendNow: true), cancellationToken);
            }
        }
    }

    public async Task<int> CreateAndDispatchAsync(NotificationCreateOptions options, CancellationToken cancellationToken = default)
    {
        var channel = NotificationChannels.Normalize(options.Channel);
        var resolved = await ResolveContentAsync(options, channel, cancellationToken);

        if (options.UserId is int uid && !await IsChannelAllowedAsync(uid, channel, options.TemplateKey, cancellationToken))
        {
            _logger.LogDebug("Skipping {Channel} for user {UserId} due to preferences", channel, uid);
            return 0;
        }

        using var connection = _dbFactory.CreateConnection();

        var (category, neverDelete) = NotificationRetention.Classify(
            options.Type, options.Module, options.TemplateKey, resolved.Title);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Notifications
                (UserId, Title, Message, Type, ReferenceId, IsRead, Priority, Channel, RecipientType,
                 IsSent, SentDate, TemplateKey, Module, RetryCount, NextRetryAt, DeliveryStatus,
                 RetentionCategory, NeverAutoDelete, IsArchived, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
                (@UserId, @Title, @Message, @Type, @ReferenceId, 0, @Priority, @Channel, @RecipientType,
                 0, NULL, @TemplateKey, @Module, 0, NULL, 'Pending',
                 @RetentionCategory, @NeverAutoDelete, 0, @CreatedAt, 0)
            """,
            new
            {
                options.UserId,
                Title = resolved.Title,
                Message = resolved.PlainMessage,
                Type = (int)options.Type,
                options.ReferenceId,
                options.Priority,
                Channel = channel,
                options.RecipientType,
                options.TemplateKey,
                Module = options.Module ?? "System",
                RetentionCategory = category,
                NeverAutoDelete = neverDelete,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken: cancellationToken));

        if (options.UserId is int recipientId)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO NotificationRecipients
                    (NotificationId, UserId, DeliveryStatus, IsRead, CreatedAt, IsArchived, IsDeleted)
                VALUES (@NotificationId, @UserId, 'Pending', 0, GETUTCDATE(), 0, 0)
                """,
                new { NotificationId = id, UserId = recipientId },
                cancellationToken: cancellationToken));
        }

        var dispatchOptions = options with
        {
            Title = resolved.Title,
            Message = resolved.EmailHtml ?? resolved.PlainMessage
        };

        if (options.SendNow)
            await DispatchOneAsync(id, dispatchOptions, cancellationToken);
        else if (channel != NotificationChannels.InApp)
            await ScheduleRetryAsync(id, 0, cancellationToken);

        if (options.UserId is int realtimeUser &&
            (channel is NotificationChannels.InApp or NotificationChannels.Browser or NotificationChannels.Push))
        {
            await _realtime.PublishToUserAsync(realtimeUser, new
            {
                id,
                userId = realtimeUser,
                title = resolved.Title,
                message = resolved.PlainMessage,
                type = options.Type,
                isRead = false,
                referenceId = options.ReferenceId,
                createdAt = DateTime.UtcNow,
                priority = options.Priority,
                channel,
                module = options.Module ?? "System",
                isSent = options.SendNow
            }, cancellationToken);

            await InvalidateUnreadCacheAsync(realtimeUser, cancellationToken);
        }

        return id;
    }

    public async Task DispatchByIdAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PendingRow>(
            new CommandDefinition("""
                SELECT Id, UserId, Title, Message, ISNULL(Channel,'InApp') AS Channel, TemplateKey,
                       ISNULL(Module,'System') AS Module, ISNULL(RetryCount,0) AS RetryCount, Priority, Type
                FROM Notifications WHERE Id = @Id AND IsDeleted = 0
                """,
                new { Id = notificationId },
                cancellationToken: cancellationToken));

        if (row is null) return;

        var options = new NotificationCreateOptions(
            row.UserId, row.Title, row.Message, (NotificationType)row.Type,
            Priority: row.Priority, Channel: row.Channel, TemplateKey: row.TemplateKey,
            Module: row.Module, SendNow: true);

        // Re-render email HTML from stored plain text + template on resend/dispatch.
        if (string.Equals(row.Channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
        {
            var resolved = await ResolveContentAsync(options, NotificationChannels.Email, cancellationToken);
            options = options with { Title = resolved.Title, Message = resolved.EmailHtml ?? resolved.PlainMessage };
        }

        await DispatchOneAsync(row.Id, options, cancellationToken);
    }

    public async Task DispatchPendingAsync(int maxBatch = 50, CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();

        // Claim a priority-ordered batch so multiple API instances don't double-send.
        var pending = (await connection.QueryAsync<PendingRow>(
            new CommandDefinition("""
                ;WITH cte AS (
                    SELECT TOP (@Max) Id
                    FROM Notifications WITH (ROWLOCK, READPAST, UPDLOCK)
                    WHERE IsDeleted = 0
                      AND ISNULL(IsArchived, 0) = 0
                      AND IsSent = 0
                      AND Channel <> @InApp
                      AND (
                            ISNULL(DeliveryStatus, 'Pending') = 'Pending'
                         OR (DeliveryStatus = 'Processing' AND UpdatedAt < DATEADD(MINUTE, -5, GETUTCDATE()))
                      )
                      AND (NextRetryAt IS NULL OR NextRetryAt <= GETUTCDATE())
                      AND ISNULL(RetryCount, 0) < @MaxRetries
                    ORDER BY ISNULL(Priority, 2) DESC, ISNULL(NextRetryAt, CreatedAt) ASC
                )
                UPDATE n SET
                    DeliveryStatus = 'Processing',
                    UpdatedAt = GETUTCDATE()
                OUTPUT
                    INSERTED.Id,
                    INSERTED.UserId,
                    INSERTED.Title,
                    INSERTED.Message,
                    ISNULL(INSERTED.Channel,'InApp') AS Channel,
                    INSERTED.TemplateKey,
                    ISNULL(INSERTED.Module,'System') AS Module,
                    ISNULL(INSERTED.RetryCount,0) AS RetryCount,
                    ISNULL(INSERTED.Priority,2) AS Priority,
                    INSERTED.Type
                FROM Notifications n
                INNER JOIN cte ON cte.Id = n.Id
                """,
            new { Max = maxBatch, InApp = NotificationChannels.InApp, MaxRetries = MaxRetries },
            cancellationToken: cancellationToken))).ToList();

        foreach (var row in pending)
        {
            var options = new NotificationCreateOptions(
                row.UserId, row.Title, row.Message, (NotificationType)row.Type,
                Priority: row.Priority, Channel: row.Channel, TemplateKey: row.TemplateKey,
                Module: row.Module, SendNow: true);

            if (string.Equals(row.Channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
            {
                var resolved = await ResolveContentAsync(options, NotificationChannels.Email, cancellationToken);
                options = options with { Title = resolved.Title, Message = resolved.EmailHtml ?? resolved.PlainMessage };
            }

            await DispatchOneAsync(row.Id, options, cancellationToken, row.RetryCount);
        }
    }

    public async Task<int> PeekHighestPendingPriorityAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT ISNULL(MAX(ISNULL(Priority, 2)), 0)
            FROM Notifications
            WHERE IsDeleted = 0
              AND ISNULL(IsArchived, 0) = 0
              AND IsSent = 0
              AND Channel <> @InApp
              AND ISNULL(DeliveryStatus,'Pending') NOT IN ('Failed','Skipped','Processing')
              AND (NextRetryAt IS NULL OR NextRetryAt <= GETUTCDATE())
            """,
            new { InApp = NotificationChannels.InApp },
            cancellationToken: cancellationToken));
    }

    public Task InvalidateUnreadCacheAsync(int? userId, CancellationToken cancellationToken = default)
    {
        if (userId is int id)
            return _cache.RemoveAsync(UnreadKey(id), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task DispatchOneAsync(
        int notificationId,
        NotificationCreateOptions options,
        CancellationToken ct,
        int currentRetry = 0)
    {
        var channel = NotificationChannels.Normalize(options.Channel);

        if (options.UserId is int uid && !await IsChannelAllowedAsync(uid, channel, options.TemplateKey, ct))
        {
            await MarkDeliveryAsync(notificationId, false, "Skipped", "Blocked by user preferences", null, currentRetry, ct);
            return;
        }

        if (channel == NotificationChannels.InApp)
        {
            await MarkDeliveryAsync(notificationId, true, "Sent", "Stored in notification inbox", "InApp", currentRetry, ct);
            return;
        }

        var sender = _senders.FirstOrDefault(s =>
            string.Equals(s.Channel, channel, StringComparison.OrdinalIgnoreCase));

        if (sender is null)
        {
            await MarkDeliveryAsync(notificationId, false, "Skipped", $"No provider for {channel}", null, currentRetry, ct);
            return;
        }

        try
        {
            var result = await sender.SendAsync(new ChannelSendRequest(
                notificationId, options.UserId, options.Title, options.Message, channel,
                options.Email, options.Phone, options.TemplateKey), ct);

            if (result.Success)
            {
                await MarkDeliveryAsync(notificationId, true, result.Status, result.Response, result.Provider ?? channel, currentRetry, ct);

                if (channel == NotificationChannels.Browser && options.UserId is int browserUser)
                {
                    await _realtime.PublishToUserAsync(browserUser, new
                    {
                        kind = "browser",
                        title = options.Title,
                        message = options.Message,
                        notificationId
                    }, ct);
                }
            }
            else
            {
                await HandleFailureAsync(notificationId, channel, result.Response, result.Provider ?? channel, currentRetry, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch notification {Id} on {Channel}", notificationId, channel);
            await HandleFailureAsync(notificationId, channel, ex.Message, channel, currentRetry, ct);
        }
    }

    private async Task HandleFailureAsync(
        int notificationId, string channel, string? response, string? provider, int currentRetry, CancellationToken ct)
    {
        var nextRetry = currentRetry + 1;
        if (nextRetry >= MaxRetries)
        {
            await MarkDeliveryAsync(notificationId, false, "Failed", response, provider, nextRetry, ct, permanentFail: true);
            return;
        }

        var delay = RetryDelays[Math.Min(nextRetry - 1, RetryDelays.Length - 1)];
        var nextAt = DateTime.UtcNow.Add(delay);

        using var connection = _dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET
                IsSent = 0,
                RetryCount = @RetryCount,
                NextRetryAt = @NextRetryAt,
                DeliveryStatus = 'Pending',
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """,
            new { Id = notificationId, RetryCount = nextRetry, NextRetryAt = nextAt },
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO NotificationDeliveryLogs
                (NotificationId, Channel, Status, Response, Provider, RetryCount, NextRetryAt, CreatedAt)
            VALUES (@NotificationId, @Channel, 'Failed', @Response, @Provider, @RetryCount, @NextRetryAt, GETUTCDATE())
            """,
            new
            {
                NotificationId = notificationId,
                Channel = channel,
                Response = response,
                Provider = provider,
                RetryCount = nextRetry,
                NextRetryAt = nextAt
            },
            cancellationToken: ct));
    }

    private async Task ScheduleRetryAsync(int id, int retryCount, CancellationToken ct)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET NextRetryAt = GETUTCDATE(), RetryCount = @RetryCount, DeliveryStatus = 'Pending'
            WHERE Id = @Id
            """,
            new { Id = id, RetryCount = retryCount },
            cancellationToken: ct));
    }

    private async Task MarkDeliveryAsync(
        int id, bool success, string status, string? response, string? provider, int retryCount,
        CancellationToken ct, bool permanentFail = false)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET
                IsSent = @IsSent,
                SentDate = CASE WHEN @IsSent = 1 THEN GETUTCDATE() ELSE SentDate END,
                RetryCount = @RetryCount,
                NextRetryAt = NULL,
                DeliveryStatus = @DeliveryStatus,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """,
            new
            {
                Id = id,
                IsSent = success,
                RetryCount = retryCount,
                DeliveryStatus = permanentFail ? "Failed" : status
            },
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET DeliveryStatus = @Status
            WHERE NotificationId = @NotificationId
            """,
            new { NotificationId = id, Status = permanentFail ? "Failed" : status },
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO NotificationDeliveryLogs
                (NotificationId, Channel, Status, Response, Provider, RetryCount, CreatedAt)
            SELECT Id, ISNULL(Channel,'InApp'), @Status, @Response, @Provider, @RetryCount, GETUTCDATE()
            FROM Notifications WHERE Id = @Id
            """,
            new { Id = id, Status = status, Response = response, Provider = provider, RetryCount = retryCount },
            cancellationToken: ct));
    }

    private async Task<ResolvedNotificationContent> ResolveContentAsync(
        NotificationCreateOptions options, string channel, CancellationToken ct)
    {
        var vars = await BuildTemplateVariablesAsync(options, ct);

        TemplateRow? template = null;
        if (!string.IsNullOrWhiteSpace(options.TemplateKey))
            template = await FindTemplateAsync(options.TemplateKey!, channel, ct);

        string title;
        string renderedBody;

        if (template is not null)
        {
            // Subject/body from compose are *content* for {{Title}}/{{Message}}, not the template shell.
            vars["Title"] = SanitizeContent(options.Title, template.TemplateName);
            vars["title"] = vars["Title"];
            vars["Message"] = SanitizeContent(options.Message, "Please open SheikhGo ERP for full details.");
            vars["message"] = vars["Message"];

            title = EmailTemplateRenderer.ApplyPlaceholders(template.Subject, vars);
            renderedBody = EmailTemplateRenderer.ApplyPlaceholders(template.Body, vars);
        }
        else
        {
            title = EmailTemplateRenderer.ApplyPlaceholders(options.Title, vars);
            renderedBody = EmailTemplateRenderer.ApplyPlaceholders(options.Message, vars);
        }

        title = EmailTemplateRenderer.ApplyPlaceholders(title, vars);
        renderedBody = EmailTemplateRenderer.ApplyPlaceholders(renderedBody, vars);

        if (EmailTemplateRenderer.LooksLikeUnresolvedTemplate(title))
            title = vars.GetValueOrDefault("Title") ?? options.Title;
        if (EmailTemplateRenderer.LooksLikeUnresolvedTemplate(renderedBody))
            renderedBody = vars.GetValueOrDefault("Message") ?? options.Message;

        // Inbox / SMS / Push store plain text only — never the full HTML document.
        var plainMessage = vars.GetValueOrDefault("Message");
        if (string.IsNullOrWhiteSpace(plainMessage)
            || EmailTemplateRenderer.LooksLikeHtmlFragment(plainMessage)
            || EmailTemplateRenderer.IsHtmlDocument(plainMessage))
        {
            plainMessage = StripHtmlToPlain(renderedBody);
        }

        if (string.IsNullOrWhiteSpace(plainMessage))
            plainMessage = SanitizeContent(options.Message, "Notification");

        if (title.Length > 500)
            title = title[..497] + "...";

        string? emailHtml = null;
        if (channel == NotificationChannels.Email)
        {
            if (EmailTemplateRenderer.IsHtmlDocument(renderedBody))
                emailHtml = renderedBody;
            else
            {
                var accent = AccentFor(options.TemplateKey, options.Priority);
                emailHtml = EmailTemplateRenderer.WrapBranded(title, renderedBody, vars, accent);
            }
        }

        return new ResolvedNotificationContent(title, plainMessage, emailHtml);
    }

    private static string StripHtmlToPlain(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        if (!EmailTemplateRenderer.LooksLikeHtmlFragment(html) && !EmailTemplateRenderer.IsHtmlDocument(html))
            return html.Trim();

        var noScripts = System.Text.RegularExpressions.Regex.Replace(
            html, @"<script[\s\S]*?</script>|<style[\s\S]*?</style>", " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var text = System.Text.RegularExpressions.Regex.Replace(noScripts, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }

    private async Task<Dictionary<string, string>> BuildTemplateVariablesAsync(
        NotificationCreateOptions options, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = SanitizeContent(options.Title, "SheikhGo Notification"),
            ["title"] = SanitizeContent(options.Title, "SheikhGo Notification"),
            ["Message"] = SanitizeContent(options.Message, ""),
            ["message"] = SanitizeContent(options.Message, ""),
            ["DateTime"] = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm") + " UTC",
            ["CompanyName"] = "SheikhGo ERP",
            ["PortalUrl"] = _configuration["Notifications:Email:PortalUrl"]
                            ?? _configuration["Portal:ReturnBaseUrl"]
                            ?? "http://localhost:4200",
            ["Priority"] = options.Priority switch
            {
                4 => "Critical",
                3 => "High",
                1 => "Low",
                _ => "Normal"
            },
            ["AlertType"] = options.TemplateKey ?? options.Type.ToString()
        };

        if (options.Variables is not null)
        {
            foreach (var kv in options.Variables)
                vars[kv.Key] = kv.Value ?? "";
        }

        if (options.UserId is int uid)
        {
            using var connection = _dbFactory.CreateConnection();
            var user = await connection.QuerySingleOrDefaultAsync<(string? FullName, string? Email)>(
                new CommandDefinition(
                    "SELECT FullName, Email FROM Users WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = uid }, cancellationToken: ct));
            if (!string.IsNullOrWhiteSpace(user.FullName))
                vars["RecipientName"] = user.FullName;
            if (!string.IsNullOrWhiteSpace(user.Email))
                vars["RecipientEmail"] = user.Email!;
        }

        vars.TryAdd("RecipientName", "there");
        return vars;
    }

    private async Task<TemplateRow?> FindTemplateAsync(string templateKey, string channel, CancellationToken ct)
    {
        using var connection = _dbFactory.CreateConnection();

        // Prefer exact channel, then Email (for HTML), then InApp, then any active row.
        var row = await connection.QuerySingleOrDefaultAsync<TemplateRow>(
            new CommandDefinition("""
                SELECT TOP 1 TemplateKey, TemplateName, Subject, Body, Channel
                FROM NotificationTemplates
                WHERE TemplateKey = @Key AND IsActive = 1 AND IsDeleted = 0
                ORDER BY
                  CASE
                    WHEN Channel = @Channel THEN 0
                    WHEN Channel = 'Email' THEN 1
                    WHEN Channel = 'InApp' THEN 2
                    ELSE 3
                  END
                """,
                new { Key = templateKey, Channel = channel },
                cancellationToken: ct));

        return row;
    }

    private static string SanitizeContent(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (EmailTemplateRenderer.LooksLikeUnresolvedTemplate(value)) return fallback;
        return value.Trim();
    }

    private static string AccentFor(string? templateKey, int priority)
    {
        if (priority >= 4 || string.Equals(templateKey, "sos_alert", StringComparison.OrdinalIgnoreCase))
            return "#dc2626";
        if (templateKey is "speed_alert" or "over_speed" or "fuel_alert" or "vehicle_offline")
            return "#ea580c";
        if (templateKey is "maintenance_reminder" or "compliance_reminder")
            return "#ca8a04";
        return "#0F766E";
    }

    private async Task<bool> IsChannelAllowedAsync(int userId, string channel, string? templateKey, CancellationToken ct)
    {
        if (channel is NotificationChannels.InApp)
            return true;

        var alertType = AlertTypeFromTemplate(templateKey);
        if (alertType is not null &&
            !await _alertAudit.IsAlertTypeEnabledAsync(userId, alertType, channel, ct))
            return false;

        using var connection = _dbFactory.CreateConnection();
        var prefs = await connection.QuerySingleOrDefaultAsync<PrefRow>(
            new CommandDefinition("""
                SELECT EmailEnabled, SmsEnabled, PushEnabled, BrowserEnabled, WhatsAppEnabled
                FROM NotificationPreferences WHERE UserId = @UserId
                """,
                new { UserId = userId },
                cancellationToken: ct));

        // Defaults: all on except WhatsApp
        if (prefs is null)
            return channel != NotificationChannels.WhatsApp;

        return channel switch
        {
            NotificationChannels.Email => prefs.EmailEnabled,
            NotificationChannels.Sms => prefs.SmsEnabled,
            NotificationChannels.Push => prefs.PushEnabled,
            NotificationChannels.Browser => prefs.BrowserEnabled,
            NotificationChannels.WhatsApp => prefs.WhatsAppEnabled,
            _ => true
        };
    }

    private static string? AlertTypeFromTemplate(string? templateKey) => templateKey switch
    {
        "sos_alert" => "sos",
        "speed_alert" => "speed_exceeded",
        "vehicle_offline" => "vehicle_offline",
        "compliance_reminder" => "compliance_reminder",
        _ => null
    };

    private static string UnreadKey(int userId) => $"notifications:unread:{userId}";

    private sealed record PendingRow(
        int Id, int? UserId, string Title, string Message, string Channel,
        string? TemplateKey, string Module, int RetryCount, int Priority, int Type);

    private sealed record TemplateRow(
        string TemplateKey, string TemplateName, string Subject, string Body, string Channel);

    private sealed record ResolvedNotificationContent(
        string Title,
        string PlainMessage,
        string? EmailHtml);

    private sealed class PrefRow
    {
        public bool EmailEnabled { get; init; }
        public bool SmsEnabled { get; init; }
        public bool PushEnabled { get; init; }
        public bool BrowserEnabled { get; init; }
        public bool WhatsAppEnabled { get; init; }
    }
}
