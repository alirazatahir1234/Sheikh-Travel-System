namespace SheikhTravelSystem.Application.Common.Interfaces;

public record AuditEventDefinitionDto(
    string EventKey,
    string DisplayName,
    string Category,
    string Severity,
    string? Description,
    int SortOrder,
    bool Visible,
    bool IsActive,
    bool IsSystem);

public record AuditEventWrite(
    string EventKey,
    string? EntityType = null,
    int? EntityId = null,
    string? Action = null,
    string? OldValues = null,
    string? NewValues = null,
    bool Success = true,
    string? Message = null,
    int? TenantId = null,
    int? UserId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CorrelationId = null);

public record AuditEventListItemDto(
    int Id,
    int TenantId,
    string? CompanyName,
    int? UserId,
    string? UserName,
    string EventKey,
    string DisplayName,
    string Category,
    string Severity,
    string? EntityType,
    int? EntityId,
    string? Action,
    bool Success,
    string? Message,
    string? IpAddress,
    DateTime CreatedOn);

public record AuditEventDetailDto(
    int Id,
    int TenantId,
    string? CompanyName,
    int? UserId,
    string? UserName,
    string EventKey,
    string DisplayName,
    string Category,
    string Severity,
    string? EntityType,
    int? EntityId,
    string? Action,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    bool Success,
    string? Message,
    DateTime CreatedOn);

public record AuditEventSearchFilter(
    int Page = 1,
    int PageSize = 20,
    int? TenantId = null,
    int? UserId = null,
    string? Category = null,
    string? EventKey = null,
    string? EntityType = null,
    int? EntityId = null,
    string? Severity = null,
    bool? Success = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null,
    /// <summary>When true, page size may go up to export cap (10k); still requires TenantId.</summary>
    bool ForExport = false);

public record AuditRetentionDto(int RetentionDays, bool AuditEnabled, string AuditLevel);

public record AuditCompanySummaryDto(bool Enabled, int RetentionDays);

/// <summary>
/// Stage 14 Audit Engine — records and queries AuditEvents; dual-writes legacy AuditLogs.
/// </summary>
public interface IAuditEngine
{
    Task RecordAsync(AuditEventWrite write, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEventDefinitionDto>> GetCatalogAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AuditEventListItemDto> Items, int Total)> SearchAsync(
        AuditEventSearchFilter filter, CancellationToken cancellationToken = default);

    Task<AuditEventDetailDto?> GetByIdAsync(int id, int? tenantId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEventListItemDto>> GetRecentAsync(
        int tenantId, int? userId = null, int take = 20, CancellationToken cancellationToken = default);

    Task<AuditRetentionDto> GetRetentionAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<AuditCompanySummaryDto> GetSafeSummaryAsync(int tenantId, CancellationToken cancellationToken = default);
}
