namespace SheikhTravelSystem.Application.Features.Settlements.DTOs;

public record SettlementDto(
    int Id,
    int DriverId,
    string? DriverName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GrossCollected,
    decimal FuelDeducted,
    decimal AllowanceAmount,
    decimal Adjustments,
    decimal Payable,
    string Status,
    DateTime? PaidAt,
    string? Reference,
    DateTime CreatedAt);

public record SettlementLineDto(
    int Id,
    string LineType,
    string Description,
    decimal Amount,
    int? ReferenceId);

public record SettlementDetailDto(
    int Id,
    int DriverId,
    string? DriverName,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GrossCollected,
    decimal FuelDeducted,
    decimal AllowanceAmount,
    decimal Adjustments,
    decimal Payable,
    string Status,
    DateTime? PaidAt,
    string? Reference,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<SettlementLineDto> Lines);

public sealed class SettlementTotals
{
    public decimal GrossCollected { get; init; }
    public decimal FuelDeducted { get; init; }
    public decimal AllowanceAmount { get; init; }
}

public sealed class SettlementLineInput
{
    public string LineType { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Amount { get; init; }
    public int? ReferenceId { get; init; }
}
