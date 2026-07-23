namespace SheikhTravelSystem.Application.Common.Interfaces;

public record SecurityPolicyDefinitionDto(
    string PolicyKey,
    string DisplayName,
    string Category,
    string? Description,
    string DefaultValue,
    string ValueType,
    int SortOrder,
    bool Visible,
    bool IsActive,
    bool IsSystem);

public record SecurityPolicyValueDto(
    string PolicyKey,
    string DisplayName,
    string Category,
    string? Description,
    string ValueType,
    string DefaultValue,
    string EffectiveValue,
    string? TenantValue,
    DateTime? UpdatedDate,
    int? UpdatedBy,
    bool IsOverridden);

public record SecurityCompanySummaryDto(
    int? PasswordExpiryDays,
    int? IdleTimeoutMinutes,
    int? AbsoluteTimeoutMinutes,
    string AuditLevel,
    bool LockoutEnabled,
    bool MfaRequired,
    bool PasswordExpired = false,
    int? PasswordDaysRemaining = null);

/// <summary>
/// Stage 13 Security Engine — resolves tenant security policies and soft-enforcement helpers.
/// Does not replace JWT/login architecture.
/// </summary>
public interface ISecurityEngine
{
    Task<IReadOnlyList<SecurityPolicyDefinitionDto>> GetCatalogAsync(
        bool activeOnly = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SecurityPolicyValueDto>> GetCompanyPoliciesAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task SetCompanyPoliciesAsync(
        int tenantId,
        IReadOnlyDictionary<string, string> values,
        int? updatedBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetEffectiveMapAsync(
        int tenantId, CancellationToken cancellationToken = default);

    Task<SecurityCompanySummaryDto> GetSafeSummaryAsync(
        int tenantId,
        DateTime? passwordChangedAt = null,
        CancellationToken cancellationToken = default);

    int GetInt(IReadOnlyDictionary<string, string> map, string key, int fallback);
    bool GetBool(IReadOnlyDictionary<string, string> map, string key, bool fallback);
    string GetString(IReadOnlyDictionary<string, string> map, string key, string fallback);

    bool IsPasswordExpired(DateTime? passwordChangedAt, int maxAgeDays);
    bool IsClientIpAllowed(string? clientIp, bool restrictEnabled, string? allowedCidrs);
    bool ShouldWriteAudit(string auditLevel, bool isError = false, bool isCritical = false);
}
