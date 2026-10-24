using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Domain.Entities;

/// <summary>
/// Deterministic operations automation alert (not generative AI).
/// </summary>
public class SmartAlert
{
    public long Id { get; set; }
    public int TenantId { get; set; }
    public SmartAlertRuleType RuleType { get; set; }
    public SmartAlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public SmartAlertStatus Status { get; set; } = SmartAlertStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    /// <summary>Unique per tenant; typically RuleType + entity + UTC date (or tighter bucket for anomalies).</summary>
    public string DeduplicationKey { get; set; } = string.Empty;
}
