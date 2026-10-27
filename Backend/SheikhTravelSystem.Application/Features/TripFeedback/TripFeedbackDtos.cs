namespace SheikhTravelSystem.Application.Features.TripFeedback;

public record TripFeedbackFormDto(
    string TripNumber,
    string? DriverFirstName,
    bool AlreadySubmitted,
    DateTime ExpiresAt);

public record SubmitTripFeedbackRequest(
    int Politeness,
    int OnTimeDelivery,
    int VehicleCondition,
    string? Comment);

public record TripClientRatingDto(
    int Id,
    int TripId,
    string? TripNumber,
    int? DriverId,
    string? DriverName,
    int Score,
    int? Politeness,
    int? OnTimeDelivery,
    int? VehicleCondition,
    string? Comment,
    string Source,
    bool IsHidden,
    DateTime CreatedAt);

public record DriverClientRatingSummaryDto(
    decimal? AverageRating,
    int ReviewCount,
    decimal? AvgPoliteness,
    decimal? AvgOnTime,
    decimal? AvgVehicle);

public record PublicDriverRatingDto(
    string DisplayName,
    decimal AverageRating,
    int ReviewCount,
    decimal? AvgPoliteness,
    decimal? AvgOnTime,
    decimal? AvgVehicle);

public record SetDriverPublicRatingRequest(bool OptOut);

public record SetTripRatingVisibilityRequest(bool Hidden);
