using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly IEnumerable<INotificationChannelSender> _senders;
    private readonly INotificationRealtimePublisher _realtime;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IDbConnectionFactory dbFactory,
        ITenantContext tenantContext,
        IEnumerable<INotificationChannelSender> senders,
        INotificationRealtimePublisher realtime,
        ILogger<NotificationService> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _senders = senders;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task CreateAsync(
        int? userId,
        string title,
        string message,
        NotificationType type,
        int? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        await CreateAndDispatchAsync(new NotificationCreateOptions(
            userId, title, message, type, referenceId,
            Channel: NotificationChannels.InApp,
            SendNow: true), cancellationToken);
    }

    public async Task CreateForAllAsync(
        string title,
        string message,
        NotificationType type,
        int? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();
        var tenantId = _tenantContext.TenantId ?? 1;
        var userIds = (await connection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT Id FROM Users WHERE IsDeleted = 0 AND IsActive = 1 AND TenantId = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        foreach (var userId in userIds)
        {
            await CreateAndDispatchAsync(new NotificationCreateOptions(
                userId, title, message, type, referenceId,
                Channel: NotificationChannels.InApp,
                SendNow: true), cancellationToken);
        }
    }

    public async Task<int> CreateAndDispatchAsync(NotificationCreateOptions options, CancellationToken cancellationToken = default)
    {
        var channel = NotificationChannels.Normalize(options.Channel);
        using var connection = _dbFactory.CreateConnection();

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO Notifications
                (UserId, Title, Message, Type, ReferenceId, IsRead, Priority, Channel, RecipientType,
                 IsSent, SentDate, TemplateKey, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES
                (@UserId, @Title, @Message, @Type, @ReferenceId, 0, @Priority, @Channel, @RecipientType,
                 0, NULL, @TemplateKey, @CreatedAt, 0)
            """,
            new
            {
                options.UserId,
                options.Title,
                options.Message,
                Type = (int)options.Type,
                options.ReferenceId,
                options.Priority,
                Channel = channel,
                options.RecipientType,
                options.TemplateKey,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken: cancellationToken));

        if (options.SendNow)
            await DispatchOneAsync(id, options, cancellationToken);

        if (options.UserId is int uid &&
            (channel is NotificationChannels.InApp or NotificationChannels.Browser or NotificationChannels.Push))
        {
            await _realtime.PublishToUserAsync(uid, new
            {
                id,
                userId = uid,
                title = options.Title,
                message = options.Message,
                type = options.Type,
                isRead = false,
                referenceId = options.ReferenceId,
                createdAt = DateTime.UtcNow,
                priority = options.Priority,
                channel,
                isSent = options.SendNow
            }, cancellationToken);
        }

        return id;
    }

    public async Task DispatchByIdAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(
            int Id, int? UserId, string Title, string Message, string Channel, string? TemplateKey)?>(
            new CommandDefinition("""
                SELECT Id, UserId, Title, Message, ISNULL(Channel,'InApp') AS Channel, TemplateKey
                FROM Notifications WHERE Id = @Id AND IsDeleted = 0
                """,
                new { Id = notificationId },
                cancellationToken: cancellationToken));

        if (row is null) return;

        var n = row.Value;
        await DispatchOneAsync(n.Id, new NotificationCreateOptions(
            n.UserId, n.Title, n.Message, NotificationType.BookingCreated,
            Channel: n.Channel, TemplateKey: n.TemplateKey, SendNow: true), cancellationToken);
    }

    public async Task DispatchPendingAsync(int maxBatch = 50, CancellationToken cancellationToken = default)
    {
        using var connection = _dbFactory.CreateConnection();
        var pending = (await connection.QueryAsync<(
            int Id, int? UserId, string Title, string Message, string Channel, string? TemplateKey)>(
            new CommandDefinition("""
                SELECT TOP (@Max) Id, UserId, Title, Message, Channel, TemplateKey
                FROM Notifications
                WHERE IsDeleted = 0 AND IsSent = 0 AND Channel <> @InApp
                ORDER BY CreatedAt ASC
                """,
            new { Max = maxBatch, InApp = NotificationChannels.InApp },
            cancellationToken: cancellationToken))).ToList();

        foreach (var row in pending)
        {
            await DispatchOneAsync(row.Id, new NotificationCreateOptions(
                row.UserId, row.Title, row.Message, NotificationType.BookingCreated,
                Channel: row.Channel, TemplateKey: row.TemplateKey, SendNow: true), cancellationToken);
        }
    }

    private async Task DispatchOneAsync(int notificationId, NotificationCreateOptions options, CancellationToken ct)
    {
        var channel = NotificationChannels.Normalize(options.Channel);

        // In-app is stored in DB — mark sent immediately.
        if (channel == NotificationChannels.InApp)
        {
            await MarkSentAsync(notificationId, true, "InApp stored", ct);
            await WriteLogAsync(notificationId, channel, "Sent", "Stored in notification inbox", ct);
            return;
        }

        var sender = _senders.FirstOrDefault(s =>
            string.Equals(s.Channel, channel, StringComparison.OrdinalIgnoreCase));

        if (sender is null)
        {
            await MarkSentAsync(notificationId, false, "No sender", ct);
            await WriteLogAsync(notificationId, channel, "Skipped", $"No provider registered for {channel}", ct);
            return;
        }

        try
        {
            var result = await sender.SendAsync(new ChannelSendRequest(
                notificationId,
                options.UserId,
                options.Title,
                options.Message,
                channel,
                options.Email,
                options.Phone,
                options.TemplateKey), ct);

            await MarkSentAsync(notificationId, result.Success, result.Response, ct);
            await WriteLogAsync(notificationId, channel, result.Status, result.Response, ct);

            if (channel == NotificationChannels.Browser && options.UserId is int uid)
            {
                await _realtime.PublishToUserAsync(uid, new
                {
                    kind = "browser",
                    title = options.Title,
                    message = options.Message,
                    notificationId
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch notification {Id} on {Channel}", notificationId, channel);
            await MarkSentAsync(notificationId, false, ex.Message, ct);
            await WriteLogAsync(notificationId, channel, "Failed", ex.Message, ct);
        }
    }

    private async Task MarkSentAsync(int id, bool success, string? response, CancellationToken ct)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications
            SET IsSent = @IsSent,
                SentDate = CASE WHEN @IsSent = 1 THEN GETUTCDATE() ELSE SentDate END,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id
            """,
            new { Id = id, IsSent = success },
            cancellationToken: ct));
        _ = response;
    }

    private async Task WriteLogAsync(int notificationId, string channel, string status, string? response, CancellationToken ct)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO NotificationDeliveryLogs (NotificationId, Channel, Status, Response, CreatedAt)
            VALUES (@NotificationId, @Channel, @Status, @Response, GETUTCDATE())
            """,
            new { NotificationId = notificationId, Channel = channel, Status = status, Response = response },
            cancellationToken: ct));
    }
}
