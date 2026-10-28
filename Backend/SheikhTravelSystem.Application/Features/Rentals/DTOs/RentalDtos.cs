namespace SheikhTravelSystem.Application.Features.Rentals.DTOs;

public static class RentalRateTypes
{
    public const string Hourly = "Hourly";
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";

    public static readonly string[] All = [Hourly, Daily, Weekly, Monthly];

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && All.Any(x => x.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class RentalBookingStatuses
{
    public const string Draft = "Draft";
    public const string Confirmed = "Confirmed";
    public const string PickedUp = "PickedUp";
    public const string Returned = "Returned";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] Blocking = [Confirmed, PickedUp];
}

public static class RentalDepositStatuses
{
    public const string Pending = "Pending";
    public const string Held = "Held";
    public const string PartiallyRefunded = "PartiallyRefunded";
    public const string Refunded = "Refunded";
    public const string Forfeited = "Forfeited";
}

public record RentalRateDto(
    int Id,
    int? VehicleId,
    string? VehicleName,
    string? RegistrationNumber,
    string RateType,
    decimal Amount,
    decimal DepositAmount,
    string Currency,
    bool IsActive,
    string? Notes);

public record CreateRentalRateDto(
    int? VehicleId,
    string RateType,
    decimal Amount,
    decimal DepositAmount = 0,
    string Currency = "PKR",
    string? Notes = null);

public record UpdateRentalRateDto(
    int? VehicleId,
    string RateType,
    decimal Amount,
    decimal DepositAmount,
    string Currency,
    bool IsActive,
    string? Notes);

public record RentalBookingListItemDto(
    int Id,
    string RentalNumber,
    int CustomerId,
    string? CustomerName,
    int VehicleId,
    string? VehicleName,
    string? RegistrationNumber,
    string RateType,
    DateTime StartAt,
    DateTime EndAt,
    decimal TotalAmount,
    decimal DepositAmount,
    string Status,
    string DepositStatus,
    string PaymentStatus);

public record RentalBookingDetailDto(
    int Id,
    string RentalNumber,
    int CustomerId,
    string? CustomerName,
    string? CustomerPhone,
    int VehicleId,
    string? VehicleName,
    string? RegistrationNumber,
    string? VehicleCategory,
    int? RateId,
    string RateType,
    decimal RateAmount,
    DateTime StartAt,
    DateTime EndAt,
    string? PickupLocation,
    string? ReturnLocation,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal DepositAmount,
    decimal DepositHeld,
    decimal DepositRefunded,
    decimal DamageCharges,
    string? DamageNotes,
    string Status,
    string DepositStatus,
    string PaymentStatus,
    decimal PaidAmount,
    string? PaymentMethod,
    string? PaymentReference,
    int? LinkedPaymentId,
    DateTime? PickedUpAt,
    int? PickupOdometer,
    string? PickupFuelLevel,
    string? PickupNotes,
    DateTime? ReturnedAt,
    int? ReturnOdometer,
    string? ReturnFuelLevel,
    string? ReturnNotes,
    string? AgreementText,
    DateTime? AgreementSignedAt,
    string? AgreementSignedBy,
    string? Notes,
    DateTime CreatedAt);

public record CreateRentalBookingDto(
    int CustomerId,
    int VehicleId,
    int? RateId,
    string RateType,
    DateTime StartAt,
    DateTime EndAt,
    string? PickupLocation = null,
    string? ReturnLocation = null,
    decimal? DiscountAmount = null,
    decimal? TotalAmount = null,
    decimal? DepositAmount = null,
    string? Notes = null,
    string? AgreementText = null);

public record UpdateRentalBookingDto(
    DateTime StartAt,
    DateTime EndAt,
    string? PickupLocation = null,
    string? ReturnLocation = null,
    string? Notes = null);

public record PickupRentalDto(
    int? Odometer,
    string? FuelLevel = null,
    string? Notes = null,
    bool HoldDeposit = true);

public record ReturnRentalDto(
    int? Odometer,
    string? FuelLevel = null,
    string? Notes = null,
    decimal DamageCharges = 0,
    string? DamageNotes = null,
    decimal? DepositRefund = null);

public record RentalQuotationDto(
    int VehicleId,
    string RateType,
    decimal RateAmount,
    int BillableUnits,
    decimal BaseCharge,
    decimal DiscountAmount,
    decimal DepositAmount,
    decimal EstimatedTotal,
    string Currency);

public record RecordRentalPaymentDto(
    decimal Amount,
    string PaymentMethod,
    string? TransactionReference = null,
    string? Notes = null);

public record SignRentalAgreementDto(
    string SignedBy,
    string? AgreementText = null);

public record RentalAvailabilitySlotDto(
    int RentalBookingId,
    string RentalNumber,
    string Status,
    DateTime StartAt,
    DateTime EndAt,
    string? CustomerName);

public record RentalAvailabilityDto(
    int VehicleId,
    string? VehicleName,
    string? RegistrationNumber,
    DateTime From,
    DateTime To,
    bool IsAvailable,
    IReadOnlyList<RentalAvailabilitySlotDto> Conflicts);

public record RentalPaymentDto(
    int Id,
    int RentalBookingId,
    decimal Amount,
    string PaymentMethod,
    string Status,
    DateTime PaymentDate,
    string? TransactionReference,
    string? Notes);
