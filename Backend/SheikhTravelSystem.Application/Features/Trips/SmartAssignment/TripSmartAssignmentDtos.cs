using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.SmartAssignment;

public record TripAssignmentScoreFactorDto(
    string Key,
    string Label,
    decimal Weight,
    decimal RawScore,
    decimal WeightedScore,
    string Detail);

public record TripAssignmentCandidateDto(
    int DriverId,
    string DriverName,
    int? VehicleId,
    string? VehicleName,
    decimal TotalScore,
    int Rank,
    decimal? DistanceKm,
    int? EtaMinutes,
    string TrafficSource,
    IReadOnlyList<TripAssignmentScoreFactorDto> Factors);

public record TripAssignmentExclusionDto(
    int DriverId,
    string DriverName,
    int? VehicleId,
    string? VehicleName,
    string Reason,
    string Detail);

public record TripSmartAssignmentSuggestionsDto(
    int TripId,
    string TripNumber,
    IReadOnlyList<TripAssignmentCandidateDto> Candidates,
    string? RoutingNote,
    string Mode = SmartAssignmentPolicy.ModeSuggestOnly,
    IReadOnlyList<TripAssignmentExclusionDto>? Excluded = null,
    IReadOnlyDictionary<string, int>? ExclusionCounts = null);

public record TripSmartAssignmentApplyResultDto(
    int TripId,
    int DriverId,
    string DriverName,
    int? VehicleId,
    string? VehicleName,
    decimal TotalScore,
    IReadOnlyList<TripAssignmentScoreFactorDto> Factors,
    int AttemptNumber);

public record TripSmartAssignmentDecisionDto(
    long Id,
    int TripId,
    string? TripNumber,
    int DriverId,
    string? DriverName,
    int? VehicleId,
    string? VehicleName,
    int Rank,
    decimal TotalScore,
    IReadOnlyList<TripAssignmentScoreFactorDto> Factors,
    decimal? DistanceKm,
    int? EtaMinutes,
    string TrafficSource,
    string Outcome,
    string? RejectionReason,
    string? CreatedBy,
    DateTime CreatedAtUtc);

public record OverrideTripSmartAssignmentRequest(int DriverId, int? VehicleId, string? Reason);

public record ConfirmTripSmartAssignmentRequest(int DriverId, int? VehicleId = null);

public record TripSmartAssignmentTripContext(
    int TripId,
    string TripNumber,
    TripStatus Status,
    DateTime PlannedStart,
    double? PickupLatitude,
    double? PickupLongitude,
    double? DestinationLatitude,
    double? DestinationLongitude,
    int? RouteId,
    int? CurrentDriverId,
    int? CurrentVehicleId,
    int PassengerCount = 1,
    TripType TripType = TripType.Transfer,
    DateTime? PlannedEnd = null,
    string? RequiredVehicleType = null);
