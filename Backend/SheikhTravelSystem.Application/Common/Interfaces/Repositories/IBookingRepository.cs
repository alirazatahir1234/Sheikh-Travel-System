using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for bookings. SQL lives in Infrastructure.
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Verifies customer/route, inserts booking, and assigns booking number.
    /// Throws <see cref="Exceptions.NotFoundException"/> when customer or route is missing.
    /// </summary>
    Task<CreateBookingResult> CreateAsync(CreateBookingDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a booking after validating status and related entities.
    /// Throws <see cref="Exceptions.NotFoundException"/> when booking, customer, or route is missing.
    /// </summary>
    Task<BookingMutationResult> UpdateAsync(int id, UpdateBookingDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a booking. Throws <see cref="Exceptions.NotFoundException"/> when missing.
    /// </summary>
    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes multiple bookings. Returns rows affected.
    /// </summary>
    Task<int> SoftDeleteManyAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a driver after conflict checks.
    /// Throws <see cref="Exceptions.NotFoundException"/> when booking or driver is missing.
    /// </summary>
    Task<BookingMutationResult> AssignDriverAsync(int bookingId, int driverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a vehicle after conflict checks.
    /// Throws <see cref="Exceptions.NotFoundException"/> when booking or vehicle is missing.
    /// </summary>
    Task<BookingMutationResult> AssignVehicleAsync(int bookingId, int vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions booking status and syncs driver/vehicle status in one transaction.
    /// Throws <see cref="Exceptions.NotFoundException"/> when booking is missing.
    /// </summary>
    Task<UpdateBookingStatusResult> UpdateStatusAsync(
        int id,
        BookingStatus status,
        string? cancellationReason,
        CancellationToken cancellationToken = default);

    Task<PagedResult<BookingDto>> GetPagedAsync(
        int page,
        int pageSize,
        BookingStatus? status,
        string? search,
        DateTime? dateFrom,
        DateTime? dateTo,
        decimal? amountMin,
        decimal? amountMax,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws <see cref="Exceptions.NotFoundException"/> when booking is missing.
    /// </summary>
    Task<BookingDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

public sealed record CreateBookingResult(int Id, string BookingNumber, string RouteName);

public sealed record BookingMutationResult(bool Success, string? ErrorMessage = null)
{
    public static BookingMutationResult Ok() => new(true);
    public static BookingMutationResult Fail(string message) => new(false, message);
}

public sealed record UpdateBookingStatusResult(
    bool Success,
    BookingStatus? PreviousStatus = null,
    string? ErrorMessage = null,
    string? SuccessMessage = null)
{
    public static UpdateBookingStatusResult Ok(BookingStatus previous, string message) =>
        new(true, previous, SuccessMessage: message);

    public static UpdateBookingStatusResult Fail(string message) =>
        new(false, ErrorMessage: message);
}
