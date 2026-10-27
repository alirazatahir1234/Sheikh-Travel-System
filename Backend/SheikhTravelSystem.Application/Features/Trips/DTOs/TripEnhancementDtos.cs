namespace SheikhTravelSystem.Application.Features.Trips.DTOs;

public record BulkTripImportRowDto(
    string TripName,
    int CustomerId,
    string TripType,
    string Priority,
    string? PickupAddress,
    string? DestinationAddress,
    DateTime TripDate,
    DateTime PlannedStart,
    int? DriverId,
    int? VehicleId,
    string? SpecialCargoFlags);

public record BulkTripImportResultDto(int Created, int Failed, IReadOnlyList<string> Errors);

public record TripTemplateDto(
    int Id,
    string Name,
    string RecurrenceKind,
    int RecurrenceInterval,
    string? RecurrenceDaysOfWeek,
    DateTime? NextOccurrenceDate,
    bool IsActive,
    DateTime CreatedAt);

public record UpsertTripTemplateDto(
    int? Id,
    string Name,
    CreateTripDto Payload,
    string RecurrenceKind,
    int RecurrenceInterval,
    string? RecurrenceDaysOfWeek,
    DateTime? NextOccurrenceDate,
    bool IsActive);

public record TripCostEstimateRequestDto(
    int? RouteId,
    int? VehicleId,
    decimal? PlannedDistanceKm,
    bool IsRoundTrip,
    decimal FuelPricePerLiter,
    decimal DriverAllowance,
    decimal TollCharges,
    decimal OtherCharges);

public record TripCostEstimateDto(
    decimal? DistanceKm,
    decimal? FuelCost,
    decimal DriverAllowance,
    decimal TollCharges,
    decimal OtherCharges,
    decimal TotalEstimatedCost,
    string Source,
    string? Note);

public record TripInsightsDto(
    string? InvoicePaymentStatus,
    decimal? InvoiceAmount,
    string FeedbackStatus,
    TripProfitabilityDto Profitability,
    TripRouteDeviationDto RouteDeviation,
    IReadOnlyList<TripLiveEventDto> LiveEvents,
    IReadOnlyList<TripDriverRejectDto> DriverRejects,
    TripAssignmentConflictDto? AssignmentConflict,
    IReadOnlyList<TripDocumentDto> ProofOfDeliveryDocuments);

public record TripProfitabilityDto(
    decimal? EstimatedRevenue,
    decimal TotalExpenses,
    decimal? Margin,
    decimal? MarginPercent);

public record TripRouteDeviationDto(
    decimal? PlannedDistanceKm,
    decimal? ActualDistanceKm,
    decimal? DeviationKm,
    decimal? DeviationPercent,
    string Summary);

public record TripLiveEventDto(
    string EventType,
    string Title,
    string? Detail,
    DateTime OccurredAtUtc,
    string Source);

public record TripDriverRejectDto(
    int DriverId,
    string? DriverName,
    string? RejectionReason,
    DateTime RejectedAtUtc);

public record TripAssignmentConflictDto(
    bool HasConflict,
    string? Message,
    int? ConflictingTripId,
    string? ConflictingTripNumber);

public record SendDelayedEtaRequestDto(string Channel, string? CustomMessage);

public record DispatchBoardMoveDto(int TripId, string TargetColumnKey);
