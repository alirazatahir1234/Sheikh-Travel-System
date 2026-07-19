using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Trips.DTOs;

public record TripListItemDto(
    int Id,
    string TripNumber,
    int? BookingId,
    string? BookingNumber,
    int CustomerId,
    string? CustomerName,
    int? DriverId,
    string? DriverName,
    int? VehicleId,
    string? VehicleName,
    int? RouteId,
    string? RouteName,
    string? PickupAddress,
    string? DestinationAddress,
    DateTime TripDate,
    DateTime PlannedStart,
    DateTime? PlannedEnd,
    TripStatus Status,
    bool GpsOnline,
    TripType TripType,
    TripPriority Priority);

public record TripStopDto(
    int Id,
    int Sequence,
    string Location,
    double? Latitude,
    double? Longitude,
    DateTime? Eta,
    DateTime? ArrivalTime,
    DateTime? DepartureTime);

public record TripStatusHistoryDto(
    int Id,
    TripStatus? FromStatus,
    TripStatus ToStatus,
    DateTime ChangedAtUtc,
    string? ChangedBy,
    string? Note);

public record TripDetailDto(
    int Id,
    string TripNumber,
    int? BookingId,
    string? BookingNumber,
    int CustomerId,
    string? CustomerName,
    int? RouteId,
    string? RouteName,
    string TripName,
    TripType TripType,
    string? PickupAddress,
    double? PickupLatitude,
    double? PickupLongitude,
    string? DestinationAddress,
    double? DestinationLatitude,
    double? DestinationLongitude,
    DateTime TripDate,
    DateTime PlannedStart,
    DateTime? PlannedEnd,
    int? EstimatedDurationMinutes,
    int? DriverId,
    string? DriverName,
    int? AssistantDriverId,
    string? AssistantDriverName,
    int? VehicleId,
    string? VehicleName,
    int PassengerCount,
    TripPriority Priority,
    TripStatus Status,
    string? DriverNotes,
    decimal? PlannedDistanceKm,
    decimal? ActualDistanceKm,
    DateTime? ActualStart,
    DateTime? ActualEnd,
    string? CancellationReason,
    bool GpsOnline,
    DateTime CreatedAt,
    IReadOnlyList<TripStopDto> Stops,
    IReadOnlyList<TripStatusHistoryDto> Timeline,
    IReadOnlyList<TripExpenseDto> Expenses,
    IReadOnlyList<TripDocumentDto> Documents,
    IReadOnlyList<TripPassengerDto> Passengers,
    int OpenAlertCount);

public record TripExpenseDto(
    int Id,
    string ExpenseType,
    decimal Amount,
    string? Description,
    DateTime ExpenseDate,
    DateTime CreatedAt);

public record TripDocumentDto(
    int Id,
    string DocumentType,
    string FileName,
    string FileUrl,
    string? UploadedBy,
    DateTime CreatedAt);

public record TripPassengerDto(
    int Id,
    string FullName,
    string? Phone,
    string BoardingStatus,
    string DropStatus,
    string? Notes);

public record CreateTripExpenseDto(string ExpenseType, decimal Amount, string? Description, DateTime? ExpenseDate);
public record CreateTripPassengerDto(string FullName, string? Phone, string? Notes);
public record UpdateTripPassengerDto(string FullName, string? Phone, string BoardingStatus, string DropStatus, string? Notes);

public record TripDashboardDto(
    int TotalTrips,
    int ScheduledTrips,
    int OngoingTrips,
    int CompletedTrips,
    int CancelledTrips,
    int DelayedTrips,
    int TodaysTrips,
    int UpcomingTrips);

public record TripStopInputDto(
    int Sequence,
    string Location,
    double? Latitude,
    double? Longitude,
    DateTime? Eta);

public record CreateTripDto(
    string TripName,
    TripType TripType,
    int? BookingId,
    int CustomerId,
    int? RouteId,
    int PassengerCount,
    TripPriority Priority,
    string? PickupAddress,
    double? PickupLatitude,
    double? PickupLongitude,
    string? DestinationAddress,
    double? DestinationLatitude,
    double? DestinationLongitude,
    DateTime TripDate,
    DateTime PlannedStart,
    DateTime? PlannedEnd,
    int? EstimatedDurationMinutes,
    decimal? PlannedDistanceKm,
    string? DriverNotes,
    int? DriverId,
    int? AssistantDriverId,
    int? VehicleId,
    IReadOnlyList<TripStopInputDto>? Stops);

public record UpdateTripDto(
    string TripName,
    TripType TripType,
    int CustomerId,
    int? RouteId,
    int PassengerCount,
    TripPriority Priority,
    string? PickupAddress,
    double? PickupLatitude,
    double? PickupLongitude,
    string? DestinationAddress,
    double? DestinationLatitude,
    double? DestinationLongitude,
    DateTime TripDate,
    DateTime PlannedStart,
    DateTime? PlannedEnd,
    int? EstimatedDurationMinutes,
    decimal? PlannedDistanceKm,
    string? DriverNotes,
    IReadOnlyList<TripStopInputDto>? Stops);

public record AssignTripDriverDto(int DriverId, int? AssistantDriverId, string? DriverNotes);

public record AssignTripVehicleDto(int VehicleId);

public record TripRouteSummaryDto(
    int TripId,
    string TripNumber,
    int? RouteId,
    string? RouteName,
    decimal? PlannedDistanceKm,
    int? EstimatedDurationMinutes,
    decimal? ActualDistanceKm,
    decimal? RemainingDistanceKm,
    decimal? DistanceCoveredKm,
    int? EtaMinutes,
    double? LiveLatitude,
    double? LiveLongitude,
    double? LiveSpeedKmh,
    bool? Ignition,
    DateTime? LastGpsAt,
    string? GoogleMapsUrl,
    string? GoogleDirectionsUrl,
    bool HasCoordinates,
    bool CanOptimize);

// ── Phase 3: calendar / live board / analytics ──────────────────────────────

public record TripCalendarItemDto(
    int Id,
    string TripNumber,
    string TripName,
    DateTime TripDate,
    DateTime PlannedStart,
    DateTime? PlannedEnd,
    TripStatus Status,
    string? CustomerName,
    string? DriverName,
    string? VehicleName,
    TripPriority Priority);

public record TripNamedCountDto(string Name, int Count);

public record TripAnalyticsDto(
    DateTime From,
    DateTime To,
    int TotalTrips,
    int CompletedTrips,
    int CancelledTrips,
    int DelayedTrips,
    int OngoingTrips,
    decimal CompletionRate,
    decimal? TotalPlannedDistanceKm,
    decimal? TotalActualDistanceKm,
    decimal TotalExpenses,
    IReadOnlyList<TripNamedCountDto> ByStatus,
    IReadOnlyList<TripNamedCountDto> ByType,
    IReadOnlyList<TripNamedCountDto> ByDriver,
    IReadOnlyList<TripNamedCountDto> ByVehicle);
