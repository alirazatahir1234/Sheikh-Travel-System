using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.SmartOps;

public record SmartAlertDto(
    long Id,
    int TenantId,
    string RuleType,
    string Severity,
    string Message,
    string? RelatedEntityType,
    int? RelatedEntityId,
    string Status,
    DateTime CreatedAt,
    DateTime? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTime? ResolvedAt,
    string? ResolvedBy,
    string DeduplicationKey);

public record SmartAlertSummaryDto(int NewCount, int ActiveCount, IReadOnlyList<SmartAlertDto> Items);

public record CreateSmartAlertRequest(
    int TenantId,
    SmartAlertRuleType RuleType,
    SmartAlertSeverity Severity,
    string Message,
    string? RelatedEntityType,
    int? RelatedEntityId,
    string DeduplicationKey);

public static class SmartAlertRuleTypeNames
{
    public const string MaintenanceDue = "MaintenanceDue";
    public const string DocumentExpiry = "DocumentExpiry";
    public const string UnassignedTrip = "UnassignedTrip";
    public const string OverdueInvoice = "OverdueInvoice";
    public const string FuelAnomaly = "FuelAnomaly";
    public const string AccidentRisk = "AccidentRisk";

    public static string ToName(SmartAlertRuleType type) => type switch
    {
        SmartAlertRuleType.MaintenanceDue => MaintenanceDue,
        SmartAlertRuleType.DocumentExpiry => DocumentExpiry,
        SmartAlertRuleType.UnassignedTrip => UnassignedTrip,
        SmartAlertRuleType.OverdueInvoice => OverdueInvoice,
        SmartAlertRuleType.FuelAnomaly => FuelAnomaly,
        SmartAlertRuleType.AccidentRisk => AccidentRisk,
        _ => type.ToString()
    };

    public static string ToName(SmartAlertSeverity severity) => severity.ToString();
    public static string ToName(SmartAlertStatus status) => status.ToString();
}
