using System.Data;
using SheikhTravelSystem.Application.Common;

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

/// <summary>
/// Write model for AuditEvents (SG-07 model A).
/// <para>
/// <b>TenantId</b> — subject/home of the record for retention and tenant-scoped search.
/// For a single-target cross-tenant read of company X: set TenantId = X (so company X
/// finds the event). For platform aggregates: set TenantId = <see cref="PlatformTenant.Id"/>.
/// TenantId is NOT NULL in the schema — never omit it; never use NULL to mean "forgot".
/// </para>
/// <para>
/// <b>TargetTenantId</b> (nullable):
/// <list type="bullet">
/// <item>One tenant's data read/mutated → TargetTenantId = that tenant id (same as TenantId for single-target).</item>
/// <item>Aggregate / all-companies list → TargetTenantId = NULL + NewValues payload with count and tenant ids.
/// Do NOT write N rows for N tenants. NULL means "platform aggregate / no single target", never "forgot to set".</item>
/// </list>
/// Rejected: burying target tenant ids only in JSON under TenantId=1 (option C).
/// </para>
/// <para>
/// <b>ActorTenantId</b> — optional home tenant of the actor; stored in NewValues envelope when set
/// (no dedicated column yet). Prefer putting actor identity in UserId.
/// </para>
/// </summary>
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
    int? TargetTenantId = null,
    int? ActorTenantId = null,
    int? UserId = null,
    string? IpAddress = null,
    string? UserAgent = null,
    string? CorrelationId = null,
    AuditFailPolicy FailPolicy = AuditFailPolicy.FailOpenLoud);

public record AuditEventListItemDto(
    int Id,
    int TenantId,
    int? TargetTenantId,
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
    int? TargetTenantId,
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
    /// <summary>When true, page size may go up to export cap (10k).</summary>
    bool ForExport = false,
    /// <summary>
    /// Platform all-companies mode (requires AuditReadAll). TenantId must be null.
    /// Matches any row; does not require a tenant context.
    /// </summary>
    bool AllCompanies = false);

public record AuditRetentionDto(int RetentionDays, bool AuditEnabled, string AuditLevel);

public record AuditCompanySummaryDto(bool Enabled, int RetentionDays);

/// <summary>
/// Stage 14 / SG-07 Audit Engine — records and queries AuditEvents; dual-writes legacy AuditLogs.
/// </summary>
public interface IAuditEngine
{
    Task RecordAsync(AuditEventWrite write, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fail-closed enrollment: INSERT into the caller's open connection/transaction.
    /// Used when the mutating work shares the same TX.
    /// </summary>
    Task RecordAsync(
        AuditEventWrite write,
        IDbConnection connection,
        IDbTransaction? transaction,
        CancellationToken cancellationToken = default);

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
