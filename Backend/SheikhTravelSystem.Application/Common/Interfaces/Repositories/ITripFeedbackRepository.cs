namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public sealed record TripFeedbackInviteRow(
    long Id,
    int TenantId,
    int TripId,
    byte[] TokenHash,
    string TokenProtected,
    DateTime ExpiresAt,
    DateTime? UsedAt);

public sealed record TripFeedbackFormContext(
    int TenantId,
    int TripId,
    string TripNumber,
    int? DriverId,
    string? DriverFirstName,
    bool AlreadySubmitted,
    DateTime ExpiresAt);

public sealed record SubmitTripFeedbackPayload(
    int Politeness,
    int OnTimeDelivery,
    int VehicleCondition,
    string? Comment);

public sealed record TripClientRatingListRow(
    int Id,
    int TripId,
    string? TripNumber,
    int? DriverId,
    string? DriverName,
    byte Score,
    byte? Politeness,
    byte? OnTimeDelivery,
    byte? VehicleCondition,
    string? Comment,
    string Source,
    bool IsHidden,
    DateTime CreatedAt);

public sealed record PublicDriverRatingRow(
    int DriverId,
    string DisplayName,
    decimal AverageRating,
    int ReviewCount,
    decimal? AvgPoliteness,
    decimal? AvgOnTime,
    decimal? AvgVehicle);

public sealed record DriverClientRatingSummary(
    decimal? AverageRating,
    int ReviewCount,
    decimal? AvgPoliteness,
    decimal? AvgOnTime,
    decimal? AvgVehicle);

public interface ITripFeedbackRepository
{
    Task<long> InsertFeedbackLinkAsync(
        int tenantId, int tripId, byte[] tokenHash, string tokenProtected, DateTime expiresAt,
        CancellationToken cancellationToken = default);

    Task<TripFeedbackInviteRow?> GetFeedbackLinkByHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default);

    Task MarkFeedbackLinkUsedAsync(long linkId, CancellationToken cancellationToken = default);

    Task<bool> HasRatingForTripAsync(int tenantId, int tripId, CancellationToken cancellationToken = default);

    Task<bool> TryInsertDetailedRatingAsync(
        int tenantId, int tripId, int? driverId, SubmitTripFeedbackPayload body, string source,
        CancellationToken cancellationToken = default);

    Task RefreshDriverAggregateRatingAsync(int tenantId, int driverId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<TripClientRatingListRow> Items, int Total)> ListRatingsAsync(
        int tenantId, int page, int pageSize, int? driverId, bool includeHidden, CancellationToken cancellationToken = default);

    Task SetRatingHiddenAsync(int tenantId, int ratingId, bool hidden, CancellationToken cancellationToken = default);

    Task SetDriverPublicOptOutAsync(int tenantId, int driverId, bool optOut, CancellationToken cancellationToken = default);

    Task<DriverClientRatingSummary?> GetDriverSummaryAsync(int tenantId, int driverId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicDriverRatingRow>> ListPublicDriverRatingsAsync(
        int tenantId, int take, CancellationToken cancellationToken = default);

    Task<TripFeedbackFormContext?> GetFeedbackFormByTripAsync(
        int tenantId, int tripId, CancellationToken cancellationToken = default);
}
