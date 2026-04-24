using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

public record DriverAllowanceRuleDto(
    int Id,
    string Name,
    AllowanceCalculationType CalculationType,
    decimal Value,
    int Priority,
    decimal? MinDistanceKm,
    decimal? MaxDistanceKm,
    FuelType? VehicleFuelType,
    string? RouteFilter,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public record CreateDriverAllowanceRuleDto(
    string Name,
    AllowanceCalculationType CalculationType,
    decimal Value,
    int Priority,
    decimal? MinDistanceKm,
    decimal? MaxDistanceKm,
    FuelType? VehicleFuelType,
    string? RouteFilter,
    string? Notes);

public record UpdateDriverAllowanceRuleDto(
    string Name,
    AllowanceCalculationType CalculationType,
    decimal Value,
    int Priority,
    decimal? MinDistanceKm,
    decimal? MaxDistanceKm,
    FuelType? VehicleFuelType,
    string? RouteFilter,
    bool IsActive,
    string? Notes);

/// <summary>
/// Request accepted by the driver-allowance evaluator. Caller may pass any of
/// the optional context fields; missing ones get sane defaults.
/// </summary>
public record CalculateDriverAllowanceRequest(
    int RouteId,
    int VehicleId,
    int? TripDays = null,
    decimal? Profit = null);

/// <summary>
/// Response returned by the evaluator — auditable: the caller always knows
/// which rule was applied, with what inputs, and how the number was produced.
/// </summary>
public record CalculateDriverAllowanceResponse(
    decimal Amount,
    int? AppliedRuleId,
    string? AppliedRuleName,
    AllowanceCalculationType? AppliedRuleType,
    decimal? AppliedRuleValue,
    string FormulaExplanation,
    bool MatchedAnyRule);
