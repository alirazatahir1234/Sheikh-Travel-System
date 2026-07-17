using Dapper;
using MediatR;
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
    DateTime? ToDate = null)
    : IRequest<ApiResponse<PagedResult<NotificationDto>>>;

public class GetNotificationsQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetNotificationsQuery, ApiResponse<PagedResult<NotificationDto>>>
{
    public async Task<ApiResponse<PagedResult<NotificationDto>>> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (request.Page - 1) * request.PageSize;

        var where = """
            WHERE (UserId = @UserId OR UserId IS NULL) AND IsDeleted = 0
            """;
        if (request.UnreadOnly == true) where += " AND IsRead = 0";
        if (request.IsSent == true) where += " AND IsSent = 1";
        if (request.IsSent == false) where += " AND IsSent = 0";
        if (!string.IsNullOrWhiteSpace(request.Channel)) where += " AND Channel = @Channel";
        if (request.Priority is not null) where += " AND Priority = @Priority";
        if (request.FromDate is not null) where += " AND CreatedAt >= @FromDate";
        if (request.ToDate is not null) where += " AND CreatedAt <= @ToDate";
        if (!string.IsNullOrWhiteSpace(request.Search))
            where += " AND (Title LIKE @Search OR Message LIKE @Search)";

        var sql = $"""
            SELECT Id, UserId, Title, Message, Type, IsRead, ReferenceId, CreatedAt,
                   ISNULL(Priority, 2) AS Priority,
                   ISNULL(Channel, 'InApp') AS Channel,
                   RecipientType, ISNULL(IsSent, 0) AS IsSent, SentDate, TemplateKey
            FROM Notifications
            {where}
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        var countSql = $"SELECT COUNT(*) FROM Notifications {where}";

        var param = new
        {
            request.UserId,
            Offset = offset,
            request.PageSize,
            request.Channel,
            request.Priority,
            request.FromDate,
            request.ToDate,
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
                ISNULL(SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END), 0) AS Unread,
                COUNT(*) AS Total,
                ISNULL(SUM(CASE WHEN Channel = 'Email' THEN 1 ELSE 0 END), 0) AS Email,
                ISNULL(SUM(CASE WHEN Channel = 'Sms' THEN 1 ELSE 0 END), 0) AS Sms,
                ISNULL(SUM(CASE WHEN Channel = 'Push' THEN 1 ELSE 0 END), 0) AS Push,
                ISNULL(SUM(CASE WHEN Channel = 'Browser' THEN 1 ELSE 0 END), 0) AS Browser,
                ISNULL(SUM(CASE WHEN Channel = 'WhatsApp' THEN 1 ELSE 0 END), 0) AS WhatsApp,
                ISNULL((SELECT COUNT(*) FROM NotificationDeliveryLogs l
                 INNER JOIN Notifications n ON n.Id = l.NotificationId
                 WHERE (n.UserId = @UserId OR n.UserId IS NULL) AND l.Status = 'Failed'), 0) AS Failed
            FROM Notifications
            WHERE (UserId = @UserId OR UserId IS NULL) AND IsDeleted = 0
            """, new { request.UserId }, cancellationToken: cancellationToken));

        return ApiResponse<NotificationStatsDto>.SuccessResponse(row);
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
            SELECT Id, TemplateKey, TemplateName, Subject, Body, Channel, IsActive
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
            SELECT COUNT(*) FROM Notifications
            WHERE Id = @NotificationId AND IsDeleted = 0
              AND (UserId = @UserId OR UserId IS NULL)
            """, new { request.NotificationId, request.UserId }, cancellationToken: cancellationToken));

        if (owned == 0)
            return ApiResponse<List<NotificationDeliveryLogDto>>.FailResponse("Notification not found.");

        var logs = (await connection.QueryAsync<NotificationDeliveryLogDto>(new CommandDefinition("""
            SELECT Id, NotificationId, Channel, Status, Response, CreatedAt
            FROM NotificationDeliveryLogs
            WHERE NotificationId = @NotificationId
            ORDER BY CreatedAt DESC
            """, new { request.NotificationId }, cancellationToken: cancellationToken))).ToList();

        return ApiResponse<List<NotificationDeliveryLogDto>>.SuccessResponse(logs);
    }
}
