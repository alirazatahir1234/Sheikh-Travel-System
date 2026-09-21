using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Notifications.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for notification mailbox, preferences, templates, and lifecycle.
/// Create/dispatch stays on <see cref="INotificationService"/>.
/// </summary>
public interface INotificationRepository
{
    Task<PagedResult<NotificationDto>> GetPagedAsync(
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
        CancellationToken cancellationToken = default);

    Task<NotificationStatsDto> GetStatsAsync(
        int tenantId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        int tenantId,
        int userId,
        string? channel,
        CancellationToken cancellationToken = default);

    Task<NotificationPreferencesDto?> GetPreferencesAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task UpsertPreferencesAsync(
        int userId,
        NotificationPreferencesDto preferences,
        CancellationToken cancellationToken = default);

    Task<List<NotificationTemplateDto>> GetTemplatesAsync(
        string? channel,
        CancellationToken cancellationToken = default);

    Task UpdateTemplateAsync(
        int id,
        UpsertNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<int> InsertTemplateAsync(
        UpsertNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UserCanAccessNotificationAsync(
        int tenantId,
        int notificationId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<List<NotificationDeliveryLogDto>> GetDeliveryLogsAsync(
        int notificationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string?>> GetRetentionSettingsAsync(
        int tenantId,
        CancellationToken cancellationToken = default);

    Task UpsertRetentionSettingsAsync(
        int tenantId,
        IReadOnlyDictionary<string, string?> settings,
        CancellationToken cancellationToken = default);

    Task<NotificationRetentionEstimateDto> GetRetentionEstimateAsync(
        DateTime archiveCutoff,
        int archivedDeleteDays,
        int failedDeleteDays,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(
        int userId,
        IReadOnlyList<int>? notificationIds,
        CancellationToken cancellationToken = default);

    Task<int> SoftDeleteAsync(
        int userId,
        IReadOnlyList<int> ids,
        CancellationToken cancellationToken = default);

    Task<int> SoftDeleteAllAsync(
        int userId,
        bool archivedOnly,
        CancellationToken cancellationToken = default);

    Task<int> EmptyTrashAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<int> ArchiveAsync(
        int userId,
        IReadOnlyList<int> ids,
        CancellationToken cancellationToken = default);

    Task<int> RestoreAsync(
        int userId,
        IReadOnlyList<int> ids,
        CancellationToken cancellationToken = default);

    Task<List<NotificationRecipientDto>> SearchRecipientsAsync(
        int tenantId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetActiveUserIdsByRoleAsync(
        int tenantId,
        int role,
        CancellationToken cancellationToken = default);

    Task<int?> FindUserIdByEmailAsync(
        int tenantId,
        string email,
        CancellationToken cancellationToken = default);
}
