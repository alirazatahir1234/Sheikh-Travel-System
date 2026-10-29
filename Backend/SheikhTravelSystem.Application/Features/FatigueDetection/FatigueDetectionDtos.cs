namespace SheikhTravelSystem.Application.Features.FatigueDetection;

public record FatigueTenantSettingsDto(
    int AckWindowSeconds,
    int StageIntervalSeconds,
    bool AutoStopEnabled);

public record UpsertFatigueTenantSettingsRequest(
    int AckWindowSeconds,
    int StageIntervalSeconds,
    bool AutoStopEnabled);

public record IngestFatigueSignalRequest(
    int? DriverId,
    int? VehicleId,
    string SignalType,
    string Source = "external_api",
    decimal? Confidence = null,
    decimal? EarScore = null,
    string? ExpressionHint = null,
    double? Latitude = null,
    double? Longitude = null,
    string? PayloadJson = null,
    decimal? ObdSpeedKmh = null,
    int? ObdHardBrakingEvents15Min = null,
    bool? ObdProlongedHighSpeed = null);

public record FatigueSignalMetrics(
    string SignalType,
    decimal? Confidence,
    decimal? EarScore,
    string? ExpressionHint,
    double? Latitude,
    double? Longitude,
    string? PayloadJson);

public record FatigueEventInsertRow(
    int TenantId,
    int DriverId,
    int? VehicleId,
    int? BookingId,
    string Source,
    FatigueSignalMetrics Metrics,
    DateTime AckDeadlineUtc,
    decimal? DriverRiskScore = null,
    decimal? CompositeRiskScore = null,
    string? SupportingSignalsJson = null);

public record FatigueEventListItemDto(
    int Id,
    int TenantId,
    string? TenantName,
    int DriverId,
    string DriverName,
    string? DriverPhone,
    int? VehicleId,
    string? VehicleName,
    string Source,
    string SignalType,
    decimal? Confidence,
    decimal? EarScore,
    string? ExpressionHint,
    double? Latitude,
    double? Longitude,
    string Status,
    int EscalationStage,
    DateTime AckDeadlineUtc,
    DateTime? AcknowledgedAtUtc,
    DateTime CreatedAtUtc,
    string? LastEscalationNote,
    decimal? DriverRiskScore,
    decimal? CompositeRiskScore,
    string? SupportingSignalsJson);

public record DriverFatigueStatusDto(
    int Id,
    string Status,
    int EscalationStage,
    DateTime AckDeadlineUtc,
    DateTime? AcknowledgedAtUtc,
    string? LastEscalationNote,
    string Message);

public record DriverFatigueActivePromptDto(
    int Id,
    string Status,
    DateTime AckDeadlineUtc,
    int SecondsRemaining,
    string PromptTitle,
    string PromptBody);

public record IngestFatigueSignalResultDto(int EventId, string Status, DateTime AckDeadlineUtc);

/// <summary>
/// Dapper row for escalation polling. Mutable class (not positional record) so nullable
/// columns and BIT map correctly — positional records fail materialization against SQL reader types.
/// </summary>
public sealed class FatigueEscalationCandidateRow
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int DriverId { get; set; }
    public int? VehicleId { get; set; }
    public string Status { get; set; } = "";
    public int EscalationStage { get; set; }
    public DateTime? NextEscalationUtc { get; set; }
    public DateTime AckDeadlineUtc { get; set; }
    public bool AutoStopEnabled { get; set; }
}
