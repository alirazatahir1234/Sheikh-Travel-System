using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Trips.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for trips. SQL lives in Infrastructure.
/// </summary>
public interface ITripRepository
{
    Task<int> CreateAsync(CreateTripDto dto, CancellationToken cancellationToken = default);

    /// <summary>Returns trip number on success for notification orchestration.</summary>
    Task<TripMutationResult> UpdateAsync(int id, UpdateTripDto dto, CancellationToken cancellationToken = default);

    Task<TripMutationResult> SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<TripStatusUpdateResult> UpdateStatusAsync(
        int id,
        TripStatus status,
        string? note,
        string? cancellationReason,
        CancellationToken cancellationToken = default);

    Task<TripMutationResult> AssignDriverAsync(
        int tripId,
        int driverId,
        int? assistantDriverId,
        string? driverNotes,
        CancellationToken cancellationToken = default);

    Task<TripMutationResult> AssignVehicleAsync(
        int tripId,
        int vehicleId,
        CancellationToken cancellationToken = default);

    Task<TripFromBookingSeedResult> GetCreateFromBookingSeedAsync(
        int bookingId,
        CancellationToken cancellationToken = default);

    Task<TripDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<TripListItemDto>> GetPagedAsync(
        int page,
        int pageSize,
        TripStatus? status,
        int? driverId,
        int? vehicleId,
        int? routeId,
        int? customerId,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? search,
        bool todayOnly,
        bool tomorrowOnly,
        bool upcomingOnly,
        CancellationToken cancellationToken = default);

    Task<TripDetailLoadResult> GetDetailAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripCalendarItemDto>> GetCalendarAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripListItemDto>> GetLiveAsync(
        bool todayOnly,
        CancellationToken cancellationToken = default);

    Task<TripAnalyticsDto> GetAnalyticsAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default);

    Task<TripRouteSummaryDto> GetRouteSummaryAsync(int tripId, CancellationToken cancellationToken = default);

    Task<TripMutationResult> OptimizeRouteAsync(int tripId, CancellationToken cancellationToken = default);

    Task EnsureTripExistsAsync(int tripId, CancellationToken cancellationToken = default);

    Task<int> AddExpenseAsync(int tripId, CreateTripExpenseDto expense, CancellationToken cancellationToken = default);

    Task SoftDeleteExpenseAsync(int tripId, int expenseId, CancellationToken cancellationToken = default);

    Task<int> AddPassengerAsync(int tripId, CreateTripPassengerDto passenger, CancellationToken cancellationToken = default);

    Task UpdatePassengerAsync(int tripId, int passengerId, UpdateTripPassengerDto passenger, CancellationToken cancellationToken = default);

    Task SoftDeletePassengerAsync(int tripId, int passengerId, CancellationToken cancellationToken = default);

    Task<int> AddDocumentAsync(
        int tripId,
        string documentType,
        string fileName,
        string storageKey,
        string? uploadedBy,
        CancellationToken cancellationToken = default);

    Task SoftDeleteDocumentAsync(int tripId, int documentId, CancellationToken cancellationToken = default);
}

public sealed record TripMutationResult(bool Success, string? ErrorMessage = null, string? TripNumber = null)
{
    public static TripMutationResult Ok(string? tripNumber = null) => new(true, null, tripNumber);
    public static TripMutationResult Fail(string message) => new(false, message);
}

public sealed record TripStatusUpdateResult(
    bool Success,
    string? ErrorMessage,
    string? TripNumber,
    TripStatus? PreviousStatus,
    TripStatus? NewStatus)
{
    public static TripStatusUpdateResult Ok(string? tripNumber, TripStatus from, TripStatus to)
        => new(true, null, tripNumber, from, to);
    public static TripStatusUpdateResult Fail(string message)
        => new(false, message, null, null, null);
}

public sealed record TripFromBookingSeedResult(
    int? ExistingTripId,
    CreateTripDto? Seed);

public sealed record TripDetailLoadResult(
    TripDetailDto Detail,
    IReadOnlyList<(int Id, string DocumentType, string FileName, string StorageKey, string? UploadedBy, DateTime CreatedAt)> Documents);