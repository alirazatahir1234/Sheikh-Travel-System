using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Application.Features.Notifications.Queries;

public record GetNotificationsQuery(
    int TenantId,
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

public class GetNotificationsQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetNotificationsQuery, ApiResponse<PagedResult<NotificationDto>>>
{
    public async Task<ApiResponse<PagedResult<NotificationDto>>> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var (fromDate, toDate) = ResolveDateRange(request.DatePreset, request.FromDate, request.ToDate);

        var result = await notificationRepository.GetPagedAsync(
            request.TenantId,
            request.UserId,
            request.Page,
            request.PageSize,
            request.UnreadOnly,
            request.IsSent,
            request.Channel,
            request.Priority,
            request.Search,
            fromDate,
            toDate,
            request.Module,
            request.Archived,
            request.Trash,
            cancellationToken);

        return ApiResponse<PagedResult<NotificationDto>>.SuccessResponse(result);
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

public record GetNotificationStatsQuery(int TenantId, int UserId) : IRequest<ApiResponse<NotificationStatsDto>>;

public class GetNotificationStatsQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetNotificationStatsQuery, ApiResponse<NotificationStatsDto>>
{
    public async Task<ApiResponse<NotificationStatsDto>> Handle(
        GetNotificationStatsQuery request, CancellationToken cancellationToken)
    {
        var row = await notificationRepository.GetStatsAsync(
            request.TenantId, request.UserId, cancellationToken);
        return ApiResponse<NotificationStatsDto>.SuccessResponse(row);
    }
}

public record GetUnreadNotificationCountQuery(
    int TenantId,
    int UserId,
    string? Channel = null) : IRequest<ApiResponse<int>>;

public class GetUnreadNotificationCountQueryHandler(
    INotificationRepository notificationRepository,
    IDistributedCache cache)
    : IRequestHandler<GetUnreadNotificationCountQuery, ApiResponse<int>>
{
    private static string CacheKey(int userId, string? channel) =>
        $"notifications:unread:{userId}:{channel ?? "inbox"}";

    public async Task<ApiResponse<int>> Handle(
        GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var cached = await cache.GetStringAsync(CacheKey(request.UserId, request.Channel), cancellationToken);
            if (int.TryParse(cached, out var cachedCount))
                return ApiResponse<int>.SuccessResponse(cachedCount);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Redis optional — continue with DB
        }

        var count = await notificationRepository.GetUnreadCountAsync(
            request.TenantId, request.UserId, request.Channel, cancellationToken);

        try
        {
            await cache.SetStringAsync(
                CacheKey(request.UserId, request.Channel),
                count.ToString(),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) },
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // ignore cache write failures
        }

        return ApiResponse<int>.SuccessResponse(count);
    }
}

public record GetNotificationPreferencesQuery(int UserId) : IRequest<ApiResponse<NotificationPreferencesDto>>;

public class GetNotificationPreferencesQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetNotificationPreferencesQuery, ApiResponse<NotificationPreferencesDto>>
{
    public async Task<ApiResponse<NotificationPreferencesDto>> Handle(
        GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var row = await notificationRepository.GetPreferencesAsync(request.UserId, cancellationToken);
        return ApiResponse<NotificationPreferencesDto>.SuccessResponse(
            row ?? new NotificationPreferencesDto());
    }
}

public record GetNotificationTemplatesQuery(string? Channel = null)
    : IRequest<ApiResponse<List<NotificationTemplateDto>>>;

public class GetNotificationTemplatesQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetNotificationTemplatesQuery, ApiResponse<List<NotificationTemplateDto>>>
{
    public async Task<ApiResponse<List<NotificationTemplateDto>>> Handle(
        GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await notificationRepository.GetTemplatesAsync(request.Channel, cancellationToken);
        return ApiResponse<List<NotificationTemplateDto>>.SuccessResponse(rows);
    }
}

public record GetNotificationHistoryQuery(int TenantId, int NotificationId, int UserId)
    : IRequest<ApiResponse<List<NotificationDeliveryLogDto>>>;

public class GetNotificationHistoryQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetNotificationHistoryQuery, ApiResponse<List<NotificationDeliveryLogDto>>>
{
    public async Task<ApiResponse<List<NotificationDeliveryLogDto>>> Handle(
        GetNotificationHistoryQuery request, CancellationToken cancellationToken)
    {
        var canAccess = await notificationRepository.UserCanAccessNotificationAsync(
            request.TenantId, request.NotificationId, request.UserId, cancellationToken);

        if (!canAccess)
            return ApiResponse<List<NotificationDeliveryLogDto>>.FailResponse("Notification not found.");

        var logs = await notificationRepository.GetDeliveryLogsAsync(
            request.NotificationId, cancellationToken);
        return ApiResponse<List<NotificationDeliveryLogDto>>.SuccessResponse(logs);
    }
}

public record GetNotificationRetentionQuery(int TenantId) : IRequest<ApiResponse<NotificationRetentionDto>>;

public class GetNotificationRetentionQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetNotificationRetentionQuery, ApiResponse<NotificationRetentionDto>>
{
    public async Task<ApiResponse<NotificationRetentionDto>> Handle(
        GetNotificationRetentionQuery request, CancellationToken cancellationToken)
    {
        var dict = await notificationRepository.GetRetentionSettingsAsync(
            request.TenantId, cancellationToken);
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

public class GetNotificationRetentionEstimateQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetNotificationRetentionEstimateQuery, ApiResponse<NotificationRetentionEstimateDto>>
{
    public async Task<ApiResponse<NotificationRetentionEstimateDto>> Handle(
        GetNotificationRetentionEstimateQuery request, CancellationToken cancellationToken)
    {
        var settings = await notificationRepository.GetRetentionSettingsAsync(
            request.TenantId, cancellationToken);
        var policy = NotificationRetentionPolicy.FromDictionary(settings);
        var archiveCutoff = DateTime.UtcNow.AddDays(-policy.ReadArchiveDays);

        var estimate = await notificationRepository.GetRetentionEstimateAsync(
            archiveCutoff,
            policy.ArchivedDeleteDays,
            policy.FailedDeleteDays,
            cancellationToken);

        return ApiResponse<NotificationRetentionEstimateDto>.SuccessResponse(estimate);
    }
}
