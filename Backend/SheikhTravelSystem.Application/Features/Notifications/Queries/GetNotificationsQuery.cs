using Dapper;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Application.Features.Notifications.Queries;

public record GetNotificationsQuery(
    int UserId,
    int Page = 1,
    int PageSize = 20,
    bool? UnreadOnly = null,
    bool? IsSent = null,
    string? Channel = null,
    int? Priority = null,
    string? Search = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Module = null,
    bool Archived = false,
    string? DatePreset = null,
    bool Trash = false)
    : IRequest<ApiResponse<PagedResult<NotificationDto>>>;

public class GetNotificationsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationsQuery, ApiResponse<PagedResult<NotificationDto>>>
{
    public async Task<ApiResponse<PagedResult<NotificationDto>>> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;
        var (fromDate, toDate) = ResolveDateRange(request.DatePreset, request.FromDate, request.ToDate);

        // Recipient-scoped mailbox with legacy fallback (owned row, no recipient yet).
        var mailbox = request.Trash
            ? """
              AND (
                    (r.Id IS NOT NULL AND r.IsDeleted = 1)
                 OR (r.Id IS NULL AND n.UserId = @UserId AND n.IsDeleted = 1)
              )
              """
            : request.Archived
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
                 OR (n.UserId IS NULL AND NOT EXISTS (
                        SELECT 1 FROM NotificationRecipients rx WHERE rx.NotificationId = n.Id))
                  )
            {mailbox}
            """;

        if (request.UnreadOnly == true)
            where += " AND ISNULL(r.IsRead, n.IsRead) = 0";
        if (request.IsSent == true) where += " AND n.IsSent = 1";
        if (request.IsSent == false) where += " AND n.IsSent = 0";
        if (!string.IsNullOrWhiteSpace(request.Channel)) where += " AND n.Channel = @Channel";
        if (request.Priority is not null) where += " AND n.Priority = @Priority";
        if (fromDate is not null) where += " AND n.CreatedAt >= @FromDate";
        if (toDate is not null) where += " AND n.CreatedAt <= @ToDate";
        if (!string.IsNullOrWhiteSpace(request.Module)) where += " AND ISNULL(n.Module,'System') = @Module";
        if (!string.IsNullOrWhiteSpace(request.Search))
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
            request.UserId,
            Offset = offset,
            request.PageSize,
            request.Channel,
            request.Priority,
            FromDate = fromDate,
            ToDate = toDate,
            request.Module,
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search}%"
        };

        var items = (await connection.QueryAsync<NotificationDto>(
            new CommandDefinition(sql, param, cancellationToken: cancellationToken))).ToList();
        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, param, cancellationToken: cancellationToken));

        return ApiResponse<PagedResult<NotificationDto>>.SuccessResponse(new PagedResult<NotificationDto>
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    private static (DateTime? From, DateTime? To) ResolveDateRange(string? preset, DateTime? from, DateTime? to)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return (from, to);

        var now = DateTime.UtcNow;
        return preset.Trim().ToLowerInvariant() switch
        {
            "today" => (now.Date, now.Date.AddDays(1).AddTicks(-1)),
            "yesterday" => (now.Date.AddDays(-1), now.Date.AddTicks(-1)),
            "7d" or "last7days" => (now.Date.AddDays(-7), now),
            "30d" or "last30days" => (now.Date.AddDays(-30), now),
            _ => (from, to)
        };
    }
}

public record GetNotificationStatsQuery(int UserId) : IRequest<ApiResponse<NotificationStatsDto>>;

public class GetNotificationStatsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationStatsQuery, ApiResponse<NotificationStatsDto>>
{
    public async Task<ApiResponse<NotificationStatsDto>> Handle(
        GetNotificationStatsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleAsync<NotificationStatsDto>(new CommandDefinition("""
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
                 WHERE (xr.UserId = @UserId OR xn.UserId = @UserId OR xn.UserId IS NULL)
                   AND ISNULL(xr.IsDeleted, xn.IsDeleted) = 0
                   AND ISNULL(xr.IsArchived, ISNULL(xn.IsArchived,0)) = 0
                   AND l.Status = 'Failed'), 0) AS Failed
            FROM Notifications n
            LEFT JOIN NotificationRecipients r ON r.NotificationId = n.Id AND r.UserId = @UserId
            WHERE (
                    r.Id IS NOT NULL
                 OR n.UserId = @UserId
                 OR (n.UserId IS NULL AND NOT EXISTS (
                        SELECT 1 FROM NotificationRecipients rx WHERE rx.NotificationId = n.Id))
                  )
              AND ISNULL(r.IsDeleted, n.IsDeleted) = 0
              AND ISNULL(r.IsArchived, ISNULL(n.IsArchived, 0)) = 0
            """, new { request.UserId }, cancellationToken: cancellationToken));

        return ApiResponse<NotificationStatsDto>.SuccessResponse(row);
    }
}

public record GetUnreadNotificationCountQuery(int UserId) : IRequest<ApiResponse<int>>;

public class GetUnreadNotificationCountQueryHandler(IDbConnectionFactory dbFactory, IDistributedCache cache)
    : IRequestHandler<GetUnreadNotificationCountQuery, ApiResponse<int>>
{
    private static string CacheKey(int userId) => $"notifications:unread:{userId}";

    public async Task<ApiResponse<int>> Handle(
        GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var cached = await cache.GetStringAsync(CacheKey(request.UserId), cancellationToken);
        if (int.TryParse(cached, out var cachedCount))
            return ApiResponse<int>.SuccessResponse(cachedCount);

        using var connection = dbFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*)
            FROM Notifications n
            LEFT JOIN NotificationRecipients r ON r.NotificationId = n.Id AND r.UserId = @UserId
            WHERE (
                    r.Id IS NOT NULL
                 OR n.UserId = @UserId
                 OR (n.UserId IS NULL AND NOT EXISTS (
                        SELECT 1 FROM NotificationRecipients rx WHERE rx.NotificationId = n.Id))
                  )
              AND ISNULL(r.IsDeleted, n.IsDeleted) = 0
              AND ISNULL(r.IsArchived, ISNULL(n.IsArchived, 0)) = 0
              AND ISNULL(r.IsRead, n.IsRead) = 0
              AND ISNULL(n.Channel, 'InApp') IN ('InApp', 'Browser', 'Push')
            """, new { request.UserId }, cancellationToken: cancellationToken));

        await cache.SetStringAsync(
            CacheKey(request.UserId),
            count.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) },
            cancellationToken);

        return ApiResponse<int>.SuccessResponse(count);
    }
}

public record GetNotificationPreferencesQuery(int UserId) : IRequest<ApiResponse<NotificationPreferencesDto>>;

public class GetNotificationPreferencesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationPreferencesQuery, ApiResponse<NotificationPreferencesDto>>
{
    public async Task<ApiResponse<NotificationPreferencesDto>> Handle(
        GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<NotificationPreferencesDto>(new CommandDefinition("""
            SELECT EmailEnabled, SmsEnabled, PushEnabled, BrowserEnabled, WhatsAppEnabled
            FROM NotificationPreferences WHERE UserId = @UserId
            """, new { request.UserId }, cancellationToken: cancellationToken));

        return ApiResponse<NotificationPreferencesDto>.SuccessResponse(
            row ?? new NotificationPreferencesDto());
    }
}

public record GetNotificationTemplatesQuery(string? Channel = null)
    : IRequest<ApiResponse<List<NotificationTemplateDto>>>;

public class GetNotificationTemplatesQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationTemplatesQuery, ApiResponse<List<NotificationTemplateDto>>>
{
    public async Task<ApiResponse<List<NotificationTemplateDto>>> Handle(
        GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var sql = """
            SELECT Id, TemplateKey, TemplateName, Subject, Body, Channel, IsActive,
                   ISNULL(Language, 'en') AS Language, Variables
            FROM NotificationTemplates
            WHERE IsDeleted = 0
            """;
        if (!string.IsNullOrWhiteSpace(request.Channel))
            sql += " AND Channel = @Channel";
        sql += " ORDER BY TemplateName, Channel";

        var rows = (await connection.QueryAsync<NotificationTemplateDto>(
            new CommandDefinition(sql, new { request.Channel }, cancellationToken: cancellationToken))).ToList();
        return ApiResponse<List<NotificationTemplateDto>>.SuccessResponse(rows);
    }
}

public record GetNotificationHistoryQuery(int NotificationId, int UserId)
    : IRequest<ApiResponse<List<NotificationDeliveryLogDto>>>;

public class GetNotificationHistoryQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationHistoryQuery, ApiResponse<List<NotificationDeliveryLogDto>>>
{
    public async Task<ApiResponse<List<NotificationDeliveryLogDto>>> Handle(
        GetNotificationHistoryQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var owned = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Notifications n
            LEFT JOIN NotificationRecipients r ON r.NotificationId = n.Id AND r.UserId = @UserId
            WHERE n.Id = @NotificationId
              AND (r.Id IS NOT NULL OR n.UserId = @UserId OR n.UserId IS NULL)
            """, new { request.NotificationId, request.UserId }, cancellationToken: cancellationToken));

        if (owned == 0)
            return ApiResponse<List<NotificationDeliveryLogDto>>.FailResponse("Notification not found.");

        var logs = (await connection.QueryAsync<NotificationDeliveryLogDto>(new CommandDefinition("""
            SELECT Id, NotificationId, Channel, Status, Response, CreatedAt,
                   Provider, ISNULL(RetryCount, 0) AS RetryCount, NextRetryAt
            FROM NotificationDeliveryLogs
            WHERE NotificationId = @NotificationId
            ORDER BY CreatedAt DESC
            """, new { request.NotificationId }, cancellationToken: cancellationToken))).ToList();

        return ApiResponse<List<NotificationDeliveryLogDto>>.SuccessResponse(logs);
    }
}

public record GetNotificationRetentionQuery(int TenantId) : IRequest<ApiResponse<NotificationRetentionDto>>;

public class GetNotificationRetentionQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationRetentionQuery, ApiResponse<NotificationRetentionDto>>
{
    public async Task<ApiResponse<NotificationRetentionDto>> Handle(
        GetNotificationRetentionQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<(string Key, string? Value)>(new CommandDefinition("""
            SELECT [Key], Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = @Category AND IsActive = 1
            """,
            new { request.TenantId, Category = NotificationRetention.SettingsCategory },
            cancellationToken: cancellationToken))).ToList();

        var dict = rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var policy = NotificationRetentionPolicy.FromDictionary(dict);
        return ApiResponse<NotificationRetentionDto>.SuccessResponse(new NotificationRetentionDto(
            policy.ReadArchiveDays,
            policy.ArchivedDeleteDays,
            policy.FailedDeleteDays,
            policy.DraftDeleteDays,
            policy.OperationalDeleteDays,
            policy.MaintenanceDeleteDays,
            policy.ComplianceDeleteDays,
            policy.CriticalNeverDelete,
            policy.SecurityDeleteDays));
    }
}

public record GetNotificationRetentionEstimateQuery(int TenantId)
    : IRequest<ApiResponse<NotificationRetentionEstimateDto>>;

public class GetNotificationRetentionEstimateQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationRetentionEstimateQuery, ApiResponse<NotificationRetentionEstimateDto>>
{
    public async Task<ApiResponse<NotificationRetentionEstimateDto>> Handle(
        GetNotificationRetentionEstimateQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = (await connection.QueryAsync<(string Key, string? Value)>(new CommandDefinition("""
            SELECT [Key], Value FROM PlatformSettings
            WHERE TenantId = @TenantId AND Category = @Category AND IsActive = 1
            """,
            new { request.TenantId, Category = NotificationRetention.SettingsCategory },
            cancellationToken: cancellationToken))).ToList();

        var policy = NotificationRetentionPolicy.FromDictionary(
            rows.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));

        var archiveCutoff = DateTime.UtcNow.AddDays(-policy.ReadArchiveDays);
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
            new { ArchivedDays = policy.ArchivedDeleteDays, FailedDays = policy.FailedDeleteDays },
            cancellationToken: cancellationToken));

        var protectedCritical = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM Notifications WHERE ISNULL(NeverAutoDelete, 0) = 1 OR RetentionCategory = 'Critical'
            """, cancellationToken: cancellationToken));

        return ApiResponse<NotificationRetentionEstimateDto>.SuccessResponse(
            new NotificationRetentionEstimateDto(eligibleArchive, eligibleDelete, protectedCritical));
    }
}
