using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Rentals.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IRentalRepository
{
    Task<PagedResult<RentalRateDto>> GetRatesPagedAsync(
        int tenantId, int page, int pageSize, int? vehicleId, string? rateType, bool? isActive,
        CancellationToken cancellationToken = default);

    Task<RentalRateDto?> GetRateByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<int> CreateRateAsync(int tenantId, CreateRentalRateDto dto, int? createdBy, CancellationToken cancellationToken = default);

    Task<bool> UpdateRateAsync(int id, int tenantId, UpdateRentalRateDto dto, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteRateAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<RentalRateDto?> FindBestRateAsync(
        int tenantId, int vehicleId, string rateType, CancellationToken cancellationToken = default);

    Task<PagedResult<RentalBookingListItemDto>> GetBookingsPagedAsync(
        int tenantId, int page, int pageSize, string? status, int? vehicleId, int? customerId,
        CancellationToken cancellationToken = default);

    Task<RentalBookingDetailDto?> GetBookingByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> CustomerExistsAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> VehicleExistsAsync(int vehicleId, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> HasOverlappingBookingAsync(
        int tenantId, int vehicleId, DateTime startAt, DateTime endAt, int? excludeBookingId,
        CancellationToken cancellationToken = default);

    Task<int> CreateBookingAsync(
        int tenantId, CreateRentalBookingDto dto, string rentalNumber, decimal totalAmount,
        decimal depositAmount, decimal rateAmount, string? vehicleCategory, string? agreementText,
        int? createdBy, CancellationToken cancellationToken = default);

    Task<(string? Name, string? RegistrationNumber, string? Model)?> GetVehicleSnapshotAsync(
        int vehicleId, int tenantId, CancellationToken cancellationToken = default);

    Task<string?> GetCustomerNameAsync(int customerId, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> UpdateBookingScheduleAsync(
        int id, int tenantId, DateTime startAt, DateTime endAt,
        string? pickupLocation, string? returnLocation, string? notes,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateBookingStatusAsync(
        int id, int tenantId, string status, CancellationToken cancellationToken = default);

    Task<bool> MarkPickedUpAsync(
        int id, int tenantId, DateTime pickedUpAt, int? odometer, string? fuelLevel, string? notes,
        string depositStatus, decimal depositHeld, CancellationToken cancellationToken = default);

    Task<bool> MarkReturnedAsync(
        int id, int tenantId, DateTime returnedAt, int? odometer, string? fuelLevel, string? notes,
        decimal damageCharges, string? damageNotes, decimal depositRefunded, string depositStatus,
        CancellationToken cancellationToken = default);

    Task<bool> CloseBookingAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> SignAgreementAsync(
        int id, int tenantId, string signedBy, string agreementText, DateTime signedAt,
        CancellationToken cancellationToken = default);

    Task<int> RecordPaymentAsync(
        int tenantId, int rentalBookingId, RecordRentalPaymentDto dto, int? createdBy,
        CancellationToken cancellationToken = default);

    Task UpdateBookingPaymentTotalsAsync(
        int rentalBookingId, int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalPaymentDto>> GetPaymentsAsync(
        int rentalBookingId, int tenantId, CancellationToken cancellationToken = default);

    Task<RentalAvailabilityDto> CheckAvailabilityAsync(
        int tenantId, int vehicleId, DateTime from, DateTime to,
        CancellationToken cancellationToken = default);

    Task<string> NextRentalNumberAsync(int tenantId, CancellationToken cancellationToken = default);
}
