using Dapper;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(IDbConnectionFactory dbFactory) : INotificationRepository
{
    public async Task<PagedResult<NotificationDto>> GetPagedAsync(
        int tenantId,
        int userId,
        int page,
        int pageSize,
        bool? unreadOnly,
        bool? isSent,
        string? channel,
        int? priority,
        string? search,
        DateTime? fromDate,
        DateTime? toDate,
        string? module,
        bool archived,
        bool trash,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var mailbox = trash
            ? """
              AND (
                    (r.Id IS NOT NULL AND r.IsDeleted = 1)
                 OR (r.Id IS NULL AND n.UserId = @UserId AND n.IsDeleted = 1)
              )
              """
            : archived
                ? """
                  AND (
                        (r.Id IS NOT NULL AND r.IsArchived = 1 AND r.IsDeleted = 0)
                     OR (r.Id IS NULL AND n.UserId = @UserId AND ISNULL(n.IsArchived,0) = 1 AND n.IsDeleted = 0)
                  )
                  """
                : """
                  AND (
                        (r.Id IS NOT NULL AND r.IsDeleted = 0 AND r.IsArchived = 0)
                     OR (r.Id IS NULL AND (n.UserId = @UserId OR n.UserId IS NULL)
                         AND n.IsDeleted = 0 AND ISNULL(n.IsArchived,0) = 0)
                  )
                  """;

        var where = $"""
            WHERE (
                    EXISTS (SELECT 1 FROM NotificationRecipients rx
                            WHERE rx.NotificationId = n.Id AND rx.UserId = @UserId)
                 OR n.UserId = @UserId
                 OR (n.UserId IS NULL AND n.RecipientType = 'SystemAnnouncement' AND NOT EXISTS (
                        SELECT 1 FROM NotificationRecipients rx WHERE rx.NotificationId = n.Id))
                  )
              AND n.TenantId = @TenantId
            {mailbox}
            """;

        if (unreadOnly == true)
            where += " AND ISNULL(r.IsRead, n.IsRead) = 0";
        if (isSent == true) where += " AND n.IsSent = 1";
        if (isSent == false) where += " AND n.IsSent = 0";
        if (!string.IsNullOrWhiteSpace(channel)) where += " AND n.Channel = @Channel";
        if (priority is not null) where += " AND n.Priority = @Priority";
        if (fromDate is not null) where += " AND n.CreatedAt >= @FromDate";
        if (toDate is not null) where += " AND n.CreatedAt <= @ToDate";
        if (!string.IsNullOrWhiteSpace(module)) where += " AND ISNULL(n.Module,'System') = @Module";
        if (!string.IsNullOrWhiteSpace(search))
            where += " AND (n.Title LIKE @Search OR n.Message LIKE @Search)";

        var sql = $"""
            SELECT n.Id, n.UserId, n.Title, n.Message, n.Type,
                   ISNULL(r.IsRead, n.IsRead) AS IsRead,
                   n.ReferenceId, n.CreatedAt,
                   ISNULL(n.Priority, 2) AS Priority,
                   ISNULL(n.Channel, 'InApp') AS Channel,
                   n.RecipientType, ISNULL(n.IsSent, 0) AS IsSent, n.SentDate, n.TemplateKey,
                   ISNULL(n.Module, 'System') AS Module,
                   ISNULL(r.ReadAt, n.ReadDate) AS ReadDate,
                   ISNULL(r.DeliveryStatus, ISNULL(n.DeliveryStatus, CASE WHEN n.IsSent = 1 THEN 'Sent' ELSE 'Pending' END)) AS DeliveryStatus,
                   ISNULL(r.IsArchived, ISNULL(n.IsArchived, 0)) AS IsArchived,
                   ISNULL(r.IsDeleted, n.IsDeleted) AS IsDeleted,
                   ISNULL(n.RetentionCategory, 'Standard') AS RetentionCategory,
                   ISNULL(n.NeverAutoDelete, 0) AS NeverAutoDelete
            FROM Notifications n
            LEFT JOIN NotificationRecipients r ON r.NotificationId = n.Id AND r.UserId = @UserId
            {where}
            ORDER BY n.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        var countSql = $"""
            SELECT COUNT(*)
            FROM Notifications n
            LEFT JOIN NotificationRecipients r ON r.NotificationId = n.Id AND r.UserId = @UserId
            {where}
            """;

        var param = new
        {
            UserId = userId,
            TenantId = tenantId,
            Offset = offset,
            PageSize = pageSize,
            Channel = channel,
            Priority = priority,
            FromDate = fromDate,
            ToDate = toDate,
            Module = module,
            Search = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%"
        };

        var items = (await connection.QueryAsync<NotificationDto>(
            new CommandDefinition(sql, param, cancellationToken: cancellationToken))).ToList();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, param, cancellationToken: cancellationToken));

        return new PagedResult<NotificationDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<NotificationStatsDto> GetStatsAsync(
        int tenantId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleAsync<NotificationStatsDto>(new CommandDefinition("""
            SELECT
                ISNULL(SUM(CASE WHEN ISNULL(r.IsRead, n.IsRead) = 0 THEN 1 ELSE 0 END), 0) AS Unread,
                COUNT(*) AS Total,
                ISNULL(SUM(CASE WHEN n.Channel = 'Email' THEN 1 ELSE 0 END), 0) AS Email,
                ISNULL(SUM(CASE WHEN n.Channel = 'Sms' THEN 1 ELSE 0 END), 0) AS Sms,
                ISNULL(SUM(CASE WHEN n.Channel = 'Push' THEN 1 ELSE 0 END), 0) AS Push,
                ISNULL(SUM(CASE WHEN n.Channel = 'Browser' THEN 1 ELSE 0 END), 0) AS Browser,
                ISNULL(SUM(CASE WHEN n.Channel = 'WhatsApp' THEN 1 ELSE 0 END), 0) AS WhatsApp,
                ISNULL((SELECT COUNT(*) FROM NotificationDeliveryLogs l
                 INNER JOIN Notifications xn ON xn.Id = l.NotificationId
                 LEFT JOIN NotificationRecipients xr ON xr.NotificationId = xn.Id AND xr.UserId = @UserId
                 WHERE xn.TenantId = @TenantId
                   AND (xr.UserId = @UserId OR xn.UserId = @UserId
                        OR (xn.UserId IS NULL AND xn.RecipientType = 'SystemAnnouncement'))
                   AND ISNULL(xr.IsDeleted, xn.IsDeleted) = 0
                   AND ISNULL(xr.IsArchived, ISNULL(xn.IsArchived,0)) = 0
                   AND l.Status = 'Failed'), 0) AS Failed
            FROM Notifications n
            LEFT JOIN NotificationRecipients r ON r.NotificationId = n.Id AND r.UserId = @UserId
            WHERE (
                    r.Id IS NOT NULL
                 OR n.UserId = @UserId
                 OR (n.UserId IS NULL AND n.RecipientType = 'SystemAnnouncement' AND NOT EXISTS (
                        SELECT 1 FROM NotificationRecipients rx WHERE rx.NotificationId = n.Id))
                  )
              AND n.TenantId = @TenantId
              AND ISNULL(r.IsDeleted, n.IsDeleted) = 0
              AND ISNULL(r.IsArchived, ISNULL(n.IsArchived, 0)) = 0
            """, new { UserId = userId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<int> GetUnreadCountAsync(
        int tenantId,
        int userId,
        string? channel,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var channelFilter = string.IsNullOrWhiteSpace(channel)
            ? "AND ISNULL(n.Channel, 'InApp') IN ('InApp', 'Browser', 'Push')"
            : "AND ISNULL(n.Channel, 'InApp') = @Channel";

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(*)
            FROM Notifications n
            LEFT JOIN NotificationRecipients r ON r.NotificationId = n.Id AND r.UserId = @UserId
            WHERE (
                    r.Id IS NOT NULL
                 OR n.UserId = @UserId
                 OR (n.UserId IS NULL AND n.RecipientType = 'SystemAnnouncement' AND NOT EXISTS (
                        SELECT 1 FROM NotificationRecipients rx WHERE rx.NotificationId = n.Id))
                  )
              AND n.TenantId = @TenantId
              AND ISNULL(r.IsDeleted, n.IsDeleted) = 0
              AND ISNULL(r.IsArchived, ISNULL(n.IsArchived, 0)) = 0
              AND ISNULL(r.IsRead, n.IsRead) = 0
              {channelFilter}
            """, new { UserId = userId, TenantId = tenantId, Channel = channel },
            cancellationToken: cancellationToken));
    }

    public async Task<NotificationPreferencesDto?> GetPreferencesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<NotificationPreferencesDto>(new CommandDefinition("""
            SELECT EmailEnabled, SmsEnabled, PushEnabled, BrowserEnabled, WhatsAppEnabled
            FROM NotificationPreferences WHERE UserId = @UserId
            """, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task UpsertPreferencesAsync(
        int userId,
        NotificationPreferencesDto preferences,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE NotificationPreferences AS target
            USING (SELECT @UserId AS UserId) AS source
            ON target.UserId = source.UserId
            WHEN MATCHED THEN
                UPDATE SET
                    EmailEnabled = @EmailEnabled,
                    SmsEnabled = @SmsEnabled,
                    PushEnabled = @PushEnabled,
                    BrowserEnabled = @BrowserEnabled,
                    WhatsAppEnabled = @WhatsAppEnabled,
                    UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, EmailEnabled, SmsEnabled, PushEnabled, BrowserEnabled, WhatsAppEnabled, UpdatedAt)
                VALUES (@UserId, @EmailEnabled, @SmsEnabled, @PushEnabled, @BrowserEnabled, @WhatsAppEnabled, GETUTCDATE());
            """,
            new
            {
                UserId = userId,
                preferences.EmailEnabled,
                preferences.SmsEnabled,
                preferences.PushEnabled,
                preferences.BrowserEnabled,
                preferences.WhatsAppEnabled
            },
            cancellationToken: cancellationToken));
    }

    public async Task<List<NotificationTemplateDto>> GetTemplatesAsync(
        string? channel,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT Id, TemplateKey, TemplateName, Subject, Body, Channel, IsActive,
                   ISNULL(Language, 'en') AS Language, Variables
            FROM NotificationTemplates
            WHERE IsDeleted = 0
            """;
        if (!string.IsNullOrWhiteSpace(channel))
            sql += " AND Channel = @Channel";
        sql += " ORDER BY TemplateName, Channel";

        return (await connection.QueryAsync<NotificationTemplateDto>(
            new CommandDefinition(sql, new { Channel = channel }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task UpdateTemplateAsync(
        int id,
        UpsertNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationTemplates SET
                TemplateKey = @TemplateKey, TemplateName = @TemplateName,
                Subject = @Subject, Body = @Body, Channel = @Channel,
                IsActive = @IsActive, Language = @Language, Variables = @Variables,
                UpdatedAt = GETUTCDATE()
            WHERE Id = @Id AND IsDeleted = 0
            """,
            new
            {
                Id = id,
                request.TemplateKey,
                request.TemplateName,
                request.Subject,
                request.Body,
                request.Channel,
                request.IsActive,
                Language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language,
                request.Variables
            },
            cancellationToken: cancellationToken));
    }

    public async Task<int> InsertTemplateAsync(
        UpsertNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO NotificationTemplates
                (TemplateKey, TemplateName, Subject, Body, Channel, IsActive, Language, Variables, CreatedAt, IsDeleted)
            OUTPUT INSERTED.Id
            VALUES (@TemplateKey, @TemplateName, @Subject, @Body, @Channel, @IsActive, @Language, @Variables, GETUTCDATE(), 0)
            """,
            new
            {
                request.TemplateKey,
                request.TemplateName,
                request.Subject,
                request.Body,
                request.Channel,
                request.IsActive,
                Language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language,
                request.Variables
            },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UserCanAccessNotificationAsync(
        int tenantId,
        int notificationId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var owned = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Notifications n
            LEFT JOIN NotificationRecipients r ON r.NotificationId = n.Id AND r.UserId = @UserId
            WHERE n.Id = @NotificationId
              AND n.TenantId = @TenantId
              AND (r.Id IS NOT NULL OR n.UserId = @UserId OR (n.UserId IS NULL AND n.RecipientType = 'SystemAnnouncement'))
            """, new { NotificationId = notificationId, UserId = userId, TenantId = tenantId },
            cancellationToken: cancellationToken));
        return owned > 0;
    }

    public async Task<List<NotificationDeliveryLogDto>> GetDeliveryLogsAsync(
        int notificationId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<NotificationDeliveryLogDto>(new CommandDefinition("""
            SELECT Id, NotificationId, Channel, Status, Response, CreatedAt,
                   Provider, ISNULL(RetryCount, 0) AS RetryCount, NextRetryAt
            FROM NotificationDeliveryLogs
            WHERE NotificationId = @NotificationId
            ORDER BY CreatedAt DESC
            """, new { NotificationId = notificationId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetRetentionSettingsAsync(
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<(string Key, string? Value)>(new CommandDefinition("""
            SELECT [Key], Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = @Category AND IsActive = 1
            """,
            new { TenantId = tenantId, Category = NotificationRetention.SettingsCategory },
            cancellationToken: cancellationToken))).ToList();

        return rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task UpsertRetentionSettingsAsync(
        int tenantId,
        IReadOnlyDictionary<string, string?> settings,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        foreach (var (key, value) in settings)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF EXISTS (SELECT 1 FROM PlatformSettings WHERE TenantId = @TenantId AND Category = @Category AND [Key] = @Key)
                    UPDATE PlatformSettings SET Value = @Value, UpdatedAt = GETUTCDATE(), IsActive = 1
                    WHERE TenantId = @TenantId AND Category = @Category AND [Key] = @Key;
                ELSE
                    INSERT INTO PlatformSettings (TenantId, Category, [Key], Value)
                    VALUES (@TenantId, @Category, @Key, @Value);
                """,
                new
                {
                    TenantId = tenantId,
                    Category = NotificationRetention.SettingsCategory,
                    Key = key,
                    Value = value
                },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<NotificationRetentionEstimateDto> GetRetentionEstimateAsync(
        DateTime archiveCutoff,
        int archivedDeleteDays,
        int failedDeleteDays,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var eligibleArchive = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM NotificationRecipients r
            INNER JOIN Notifications n ON n.Id = r.NotificationId
            WHERE r.IsDeleted = 0 AND r.IsArchived = 0 AND r.IsRead = 1
              AND ISNULL(n.NeverAutoDelete, 0) = 0
              AND ISNULL(r.ReadAt, n.ReadDate) IS NOT NULL
              AND ISNULL(r.ReadAt, n.ReadDate) < @Cutoff
            """, new { Cutoff = archiveCutoff }, cancellationToken: cancellationToken));

        var eligibleDelete = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM NotificationRecipients r
            INNER JOIN Notifications n ON n.Id = r.NotificationId
            WHERE ISNULL(n.NeverAutoDelete, 0) = 0
              AND (
                    (r.IsDeleted = 1 AND r.DeletedAt IS NOT NULL AND r.DeletedAt < DATEADD(DAY, -@ArchivedDays, GETUTCDATE()))
                 OR (r.IsArchived = 1 AND r.ArchivedAt IS NOT NULL AND r.ArchivedAt < DATEADD(DAY, -@ArchivedDays, GETUTCDATE()))
                 OR (ISNULL(n.DeliveryStatus,'') = 'Failed' AND n.CreatedAt < DATEADD(DAY, -@FailedDays, GETUTCDATE()))
              )
            """,
            new { ArchivedDays = archivedDeleteDays, FailedDays = failedDeleteDays },
            cancellationToken: cancellationToken));

        var protectedCritical = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Notifications WHERE ISNULL(NeverAutoDelete, 0) = 1 OR RetentionCategory = 'Critical'
            """, cancellationToken: cancellationToken));

        return new NotificationRetentionEstimateDto(eligibleArchive, eligibleDelete, protectedCritical);
    }

    public async Task MarkReadAsync(
        int userId,
        IReadOnlyList<int>? notificationIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        if (notificationIds is { Count: > 0 })
        {
            foreach (var id in notificationIds.Distinct())
                await EnsureRecipientAsync(connection, id, userId, cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Notifications SET IsRead = 1, ReadDate = GETUTCDATE(), UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId AND Id IN @Ids AND IsRead = 0
                """,
                new { UserId = userId, Ids = notificationIds },
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE NotificationRecipients SET IsRead = 1, ReadAt = GETUTCDATE()
                WHERE UserId = @UserId AND NotificationId IN @Ids AND IsRead = 0 AND IsDeleted = 0
                """,
                new { UserId = userId, Ids = notificationIds },
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Notifications SET IsRead = 1, ReadDate = GETUTCDATE(), UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId AND IsRead = 0 AND IsDeleted = 0 AND ISNULL(IsArchived,0) = 0
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE NotificationRecipients SET IsRead = 1, ReadAt = GETUTCDATE()
                WHERE UserId = @UserId AND IsRead = 0 AND IsDeleted = 0 AND IsArchived = 0
                """,
                new { UserId = userId },
                cancellationToken: cancellationToken));
        }
    }

    public async Task<int> SoftDeleteAsync(
        int userId,
        IReadOnlyList<int> ids,
        CancellationToken cancellationToken = default)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToList();
        if (distinct.Count == 0) return 0;

        using var connection = dbFactory.CreateConnection();
        await EnsureRecipientsAsync(connection, userId, distinct, cancellationToken);

        var recipientRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET
                IsDeleted = 1, DeletedAt = GETUTCDATE(), DeletedBy = @UserId,
                IsArchived = 0, ArchivedAt = NULL
            WHERE UserId = @UserId AND NotificationId IN @Ids AND ISNULL(IsDeleted, 0) = 0
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: cancellationToken));

        var ownerRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET
                IsDeleted = 1, IsArchived = 0, ArchivedAt = NULL, UpdatedAt = GETUTCDATE()
            WHERE Id IN @Ids AND UserId = @UserId AND ISNULL(IsDeleted, 0) = 0
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: cancellationToken));

        var affected = Math.Max(recipientRows, ownerRows);
        return affected > 0 ? distinct.Count : 0;
    }

    public async Task<int> SoftDeleteAllAsync(
        int userId,
        bool archivedOnly,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO NotificationRecipients
                (NotificationId, UserId, DeliveryStatus, IsRead, CreatedAt, IsArchived, IsDeleted)
            SELECT n.Id, @UserId, ISNULL(n.DeliveryStatus, 'Pending'), n.IsRead, GETUTCDATE(),
                   ISNULL(n.IsArchived, 0), 0
            FROM Notifications n
            WHERE (n.UserId = @UserId OR (n.UserId IS NULL AND n.RecipientType = 'SystemAnnouncement'))
              AND ISNULL(n.IsDeleted, 0) = 0
              AND (
                    (@ArchivedOnly = 1 AND ISNULL(n.IsArchived, 0) = 1)
                 OR (@ArchivedOnly = 0 AND ISNULL(n.IsArchived, 0) = 0)
              )
              AND NOT EXISTS (
                    SELECT 1 FROM NotificationRecipients r
                    WHERE r.NotificationId = n.Id AND r.UserId = @UserId);
            """,
            new { UserId = userId, ArchivedOnly = archivedOnly ? 1 : 0 },
            cancellationToken: cancellationToken));

        var recipientRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET
                IsDeleted = 1, DeletedAt = GETUTCDATE(), DeletedBy = @UserId,
                IsArchived = 0, ArchivedAt = NULL
            WHERE UserId = @UserId
              AND ISNULL(IsDeleted, 0) = 0
              AND (
                    (@ArchivedOnly = 1 AND ISNULL(IsArchived, 0) = 1)
                 OR (@ArchivedOnly = 0 AND ISNULL(IsArchived, 0) = 0)
              );
            """,
            new { UserId = userId, ArchivedOnly = archivedOnly ? 1 : 0 },
            cancellationToken: cancellationToken));

        var ownerRows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET
                IsDeleted = 1, IsArchived = 0, ArchivedAt = NULL, UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId
              AND ISNULL(IsDeleted, 0) = 0
              AND (
                    (@ArchivedOnly = 1 AND ISNULL(IsArchived, 0) = 1)
                 OR (@ArchivedOnly = 0 AND ISNULL(IsArchived, 0) = 0)
              );
            """,
            new { UserId = userId, ArchivedOnly = archivedOnly ? 1 : 0 },
            cancellationToken: cancellationToken));

        return Math.Max(recipientRows, ownerRows);
    }

    public async Task<int> EmptyTrashAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var recipientDeleted = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM NotificationRecipients
            WHERE UserId = @UserId AND ISNULL(IsDeleted, 0) = 1;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        var ownerDeleted = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM NotificationDeliveryLogs
            WHERE NotificationId IN (
                SELECT n.Id FROM Notifications n
                WHERE n.UserId = @UserId AND ISNULL(n.IsDeleted, 0) = 1
                  AND NOT EXISTS (SELECT 1 FROM NotificationRecipients r WHERE r.NotificationId = n.Id)
            );

            DELETE FROM Notifications
            WHERE UserId = @UserId AND ISNULL(IsDeleted, 0) = 1
              AND NOT EXISTS (SELECT 1 FROM NotificationRecipients r WHERE r.NotificationId = Notifications.Id);
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return recipientDeleted + ownerDeleted;
    }

    public async Task<int> ArchiveAsync(
        int userId,
        IReadOnlyList<int> ids,
        CancellationToken cancellationToken = default)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToList();
        if (distinct.Count == 0) return 0;

        using var connection = dbFactory.CreateConnection();
        await EnsureRecipientsAsync(connection, userId, distinct, cancellationToken);

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET
                IsArchived = 1, ArchivedAt = GETUTCDATE(),
                IsDeleted = 0, DeletedAt = NULL, DeletedBy = NULL
            WHERE UserId = @UserId AND NotificationId IN @Ids
              AND ISNULL(IsDeleted, 0) = 0 AND ISNULL(IsArchived, 0) = 0
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET IsArchived = 1, ArchivedAt = GETUTCDATE(), IsDeleted = 0, UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId AND Id IN @Ids
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: cancellationToken));

        return rows > 0 ? distinct.Count : rows;
    }

    public async Task<int> RestoreAsync(
        int userId,
        IReadOnlyList<int> ids,
        CancellationToken cancellationToken = default)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToList();
        if (distinct.Count == 0) return 0;

        using var connection = dbFactory.CreateConnection();
        await EnsureRecipientsAsync(connection, userId, distinct, cancellationToken);

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE NotificationRecipients SET
                IsDeleted = 0, DeletedAt = NULL, DeletedBy = NULL,
                IsArchived = 0, ArchivedAt = NULL
            WHERE UserId = @UserId AND NotificationId IN @Ids
              AND (ISNULL(IsDeleted, 0) = 1 OR ISNULL(IsArchived, 0) = 1)
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Notifications SET IsDeleted = 0, IsArchived = 0, ArchivedAt = NULL, UpdatedAt = GETUTCDATE()
            WHERE UserId = @UserId AND Id IN @Ids
            """,
            new { UserId = userId, Ids = distinct },
            cancellationToken: cancellationToken));

        return rows > 0 ? distinct.Count : rows;
    }

    public async Task<List<NotificationRecipientDto>> SearchRecipientsAsync(
        int tenantId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<NotificationRecipientDto>(
            new CommandDefinition("""
                SELECT TOP 300 Id, FullName, Email
                FROM Users
                WHERE IsDeleted = 0 AND TenantId = @TenantId
                  AND (@Search IS NULL OR FullName LIKE @Like OR Email LIKE @Like)
                ORDER BY FullName
                """,
                new
                {
                    TenantId = tenantId,
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                    Like = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%"
                },
                cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<int>> GetActiveUserIdsByRoleAsync(
        int tenantId,
        int role,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var ids = await connection.QueryAsync<int>(new CommandDefinition("""
            SELECT Id FROM Users
            WHERE IsDeleted = 0 AND IsActive = 1 AND Role = @Role AND TenantId = @TenantId
            """, new { Role = role, TenantId = tenantId }, cancellationToken: cancellationToken));
        return ids.ToList();
    }

    public async Task<int?> FindUserIdByEmailAsync(
        int tenantId,
        string email,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 Id FROM Users WHERE Email = @Email AND IsDeleted = 0 AND TenantId = @TenantId",
            new { Email = email, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private static async Task EnsureRecipientAsync(
        System.Data.IDbConnection connection, int notificationId, int userId, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            IF NOT EXISTS (
                SELECT 1 FROM NotificationRecipients WHERE NotificationId = @NotificationId AND UserId = @UserId)
            INSERT INTO NotificationRecipients
                (NotificationId, UserId, DeliveryStatus, IsRead, CreatedAt, IsArchived, IsDeleted)
            SELECT n.Id, @UserId, ISNULL(n.DeliveryStatus, 'Pending'), n.IsRead, GETUTCDATE(), 0, 0
            FROM Notifications n
            WHERE n.Id = @NotificationId
              AND (
                    n.UserId = @UserId
                 OR (n.UserId IS NULL AND n.RecipientType = 'SystemAnnouncement')
                 OR EXISTS (
                        SELECT 1 FROM NotificationRecipients rx
                        WHERE rx.NotificationId = n.Id AND rx.UserId = @UserId)
              );
            """,
            new { NotificationId = notificationId, UserId = userId },
            cancellationToken: ct));
    }

    private static async Task EnsureRecipientsAsync(
        System.Data.IDbConnection connection, int userId, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO NotificationRecipients
                (NotificationId, UserId, DeliveryStatus, IsRead, CreatedAt, IsArchived, IsDeleted)
            SELECT n.Id, @UserId, ISNULL(n.DeliveryStatus, 'Pending'), n.IsRead, GETUTCDATE(), 0, 0
            FROM Notifications n
            WHERE n.Id IN @Ids
              AND (
                    n.UserId = @UserId
                 OR (n.UserId IS NULL AND n.RecipientType = 'SystemAnnouncement')
              )
              AND NOT EXISTS (
                    SELECT 1 FROM NotificationRecipients r
                    WHERE r.NotificationId = n.Id AND r.UserId = @UserId);
            """,
            new { UserId = userId, Ids = ids },
            cancellationToken: ct));
    }
}
