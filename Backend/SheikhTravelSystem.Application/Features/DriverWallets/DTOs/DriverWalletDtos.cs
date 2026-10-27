namespace SheikhTravelSystem.Application.Features.DriverWallets.DTOs;

public record DriverWalletPolicyDto(
    decimal MinimumCashOutAmountPkr,
    decimal ProcessingFeePercent,
    string CurrencyCode);

public record DriverWalletDto(
    int DriverId,
    string? DriverName,
    decimal Balance,
    string Currency,
    string CashOutCadence,
    DriverWalletPayoutDetailsDto PayoutDetails);

public record DriverWalletPayoutDetailsDto(
    string? BankAccountName,
    string? BankAccountNumber,
    string? BankName,
    string? JazzCashNumber,
    string? EasypaisaNumber);

public record DriverWalletLedgerEntryDto(
    int Id,
    string EntryType,
    decimal Amount,
    decimal BalanceAfter,
    string? ReferenceType,
    int? ReferenceId,
    string? Description,
    DateTime CreatedAt);

public record DriverCashOutRequestDto(
    int Id,
    int DriverId,
    string? DriverName,
    decimal GrossAmount,
    decimal ProcessingFee,
    decimal NetPayout,
    decimal ProcessingFeeRate,
    string Method,
    string? DestinationSnapshot,
    string Status,
    DateTime RequestedAt,
    DateTime? ProcessedAt,
    string? PaymentReference,
    string? RejectionReason);

public record DriverCashOutPreviewDto(
    decimal GrossAmount,
    decimal ProcessingFee,
    decimal NetPayout,
    decimal ProcessingFeePercent,
    decimal MinimumCashOutAmountPkr,
    string CurrencyCode);

public record UpdateDriverWalletPayoutDetailsDto(
    string? BankAccountName,
    string? BankAccountNumber,
    string? BankName,
    string? JazzCashNumber,
    string? EasypaisaNumber,
    string? CashOutCadence);

public record CreateDriverCashOutRequestDto(
    decimal GrossAmount,
    string Method,
    string? Notes);
