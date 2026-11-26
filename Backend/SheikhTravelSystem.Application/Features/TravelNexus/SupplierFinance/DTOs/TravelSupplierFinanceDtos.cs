using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

// ── Setup: exchange rates, rate cards, supplier commission rules ────────

public class TravelExchangeRateDto
{
    public int Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Reporting-currency units for one unit of <see cref="CurrencyCode"/>.</summary>
    public decimal RateToReporting { get; set; }
    public DateTime EffectiveFrom { get; set; }
}

public record SaveTravelExchangeRateDto(string CurrencyCode, decimal RateToReporting, DateTime EffectiveFrom);

public class TravelSupplierRateDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    /// <summary>HotelRoomNight | Transfer | Service.</summary>
    public string RateType { get; set; } = string.Empty;
    public int? HotelId { get; set; }
    public string? HotelName { get; set; }
    public string? RoomType { get; set; }
    public string? Description { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public record SaveTravelSupplierRateDto(string RateType, int? HotelId, string? RoomType, string? Description, DateTime ValidFrom, DateTime ValidTo,
    decimal Amount, string CurrencyCode, bool IsActive);

public record TravelRateQuoteRoomDto(string RoomType, int Quantity);
public record TravelRateQuoteRequestDto(int SupplierId, int HotelId, DateTime CheckIn, DateTime CheckOut, IReadOnlyList<TravelRateQuoteRoomDto> Rooms);

public class TravelRateQuoteDto
{
    public string? CurrencyCode { get; set; }
    public decimal Total { get; set; }
    public int Nights { get; set; }
    /// <summary>Nights (by room type) with no active rate card — the total excludes them.</summary>
    public IReadOnlyList<TravelRateQuoteGapDto> Missing { get; set; } = [];
    public bool Complete => Missing.Count == 0 && CurrencyCode is not null;
    /// <summary>Set when rate cards for the stay are in more than one currency (no total is given).</summary>
    public bool MixedCurrencies { get; set; }
}

public record TravelRateQuoteGapDto(string RoomType, DateTime Night);

public class TravelSupplierCommissionRuleDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    /// <summary>Flight | Hotel | Other.</summary>
    public string ServiceType { get; set; } = string.Empty;
    /// <summary>PercentOfCost | FixedPerUnit (per passenger for flights, per room-night for hotels).</summary>
    public string Basis { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }
}

public record SaveTravelSupplierCommissionRuleDto(string ServiceType, string Basis, decimal Rate, string? CurrencyCode, DateTime ValidFrom, DateTime? ValidTo, bool IsActive);

// ── Bills ───────────────────────────────────────────────────────────────

public class TravelSupplierBillListItemDto : TravelInvoiceMoney
{
    public int Id { get; set; }
    public string? BillNumber { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierReference { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime? BillDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Gross { get; set; }
    public decimal CommissionTotal { get; set; }
    /// <summary>Draft | Posted | Void.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Unpaid | PartiallyPaid | Paid | CreditDue (posted bills only).</summary>
    public string? PaymentStatus { get; set; }
    public bool Overdue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelSupplierBillDto : TravelSupplierBillListItemDto
{
    public string? Notes { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? VoidReason { get; set; }
    public int? BranchId { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelSupplierBillLineDto> Lines { get; set; } = [];
    public IReadOnlyList<TravelSupplierAllocationDto> Allocations { get; set; } = [];
    public IReadOnlyList<TravelSupplierCreditDto> Credits { get; set; } = [];
    public IReadOnlyList<TravelSupplierRefundDto> Refunds { get; set; } = [];
    public IReadOnlyList<TravelFinanceHistoryDto> History { get; set; } = [];
}

public class TravelSupplierBillLineDto
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int Sequence { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>Flight | Hotel; null for a manual line.</summary>
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    /// <summary>Booking | VisaCase | Group — set on manual lines (sourced lines take the service's booking).</summary>
    public string? ContextType { get; set; }
    public int? ContextId { get; set; }
    public string? ContextLabel { get; set; }
    public decimal Amount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetAmount { get; set; }
}

/// <summary>A supplier's service with a recorded cost, not yet on a live bill.</summary>
public class TravelBillableCostDto
{
    /// <summary>Flight | Hotel.</summary>
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public int SupplierId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int BookingId { get; set; }
    public string BookingLabel { get; set; } = string.Empty;
    public DateTime? ServiceDate { get; set; }
    /// <summary>Passengers (flight) or room-nights (hotel) — the unit of fixed commission rules.</summary>
    public int Units { get; set; }
    public bool Cancelled { get; set; }
    /// <summary>Commission suggested by the supplier's rule in force on the bill date.</summary>
    public decimal SuggestedCommission { get; set; }
    public int? OnBillId { get; set; }
}

// ── Payments, credits, refunds, payables ────────────────────────────────

public class TravelSupplierPaymentListItemDto
{
    public int Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Allocated { get; set; }
    public decimal Refunded { get; set; }
    public decimal Unallocated => Amount - Allocated - Refunded;
    public DateTime? ReconciledAt { get; set; }
    public string? BankReference { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelSupplierPaymentDto : TravelSupplierPaymentListItemDto
{
    public string? Notes { get; set; }
    public DateTime? ClearedAt { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelSupplierAllocationDto> Allocations { get; set; } = [];
    public IReadOnlyList<TravelSupplierRefundDto> Refunds { get; set; } = [];
    public IReadOnlyList<TravelFinanceHistoryDto> History { get; set; } = [];
}

public class TravelSupplierAllocationDto
{
    public int PaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
    public string Method { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public int BillId { get; set; }
    public string? BillNumber { get; set; }
    public string BillCurrencyCode { get; set; } = string.Empty;
    public decimal AmountFromPayment { get; set; }
    public string PaymentCurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal AmountApplied { get; set; }
}

public class TravelSupplierCreditDto
{
    public int Id { get; set; }
    public string CreditNumber { get; set; } = string.Empty;
    public int BillId { get; set; }
    public string? BillNumber { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? CancellationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime IssuedOn { get; set; }
}

public class TravelSupplierRefundDto
{
    public int Id { get; set; }
    public string RefundNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int? BillId { get; set; }
    public string? BillNumber { get; set; }
    public int? PaymentId { get; set; }
    public string? PaymentNumber { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTime ReceivedOn { get; set; }
    public int? CancellationId { get; set; }
}

public class TravelPayablesCurrencySummaryDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Positive balances of posted bills (all time).</summary>
    public decimal Outstanding { get; set; }
    /// <summary>Part of Outstanding past its due date (as of today).</summary>
    public decimal Overdue { get; set; }
    /// <summary>Negative balances: suppliers owe us (all time).</summary>
    public decimal SupplierCreditDue { get; set; }
    public decimal PaidThisMonth { get; set; }
    public decimal UnallocatedAdvances { get; set; }
}

public class TravelPayablesSummaryDto
{
    public IReadOnlyList<TravelPayablesCurrencySummaryDto> Currencies { get; set; } = [];
    public IReadOnlyDictionary<string, int> BillCounts { get; set; } = new Dictionary<string, int>();
    public int BillTotal { get; set; }
}

public class TravelSupplierBalanceDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public int OpenBills { get; set; }
    public decimal Billed { get; set; }
    public decimal Paid { get; set; }
    public decimal Credited { get; set; }
    public decimal Balance { get; set; }
    public decimal Overdue { get; set; }
    public decimal UnallocatedAdvances { get; set; }
}

public class TravelSupplierStatementDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>Positive = we owe the supplier.</summary>
    public decimal OpeningBalance { get; set; }
    public IReadOnlyList<TravelStatementEntryDto> Entries { get; set; } = [];
    public decimal ClosingBalance { get; set; }
    public IReadOnlyList<string> Currencies { get; set; } = [];
}

// ── Inputs ──────────────────────────────────────────────────────────────

public record SaveTravelSupplierBillLineDto(string Description, string? SourceType, int? SourceId, string? ContextType, int? ContextId,
    decimal Amount, decimal CommissionAmount);
public record CreateTravelSupplierBillDto(int SupplierId, string CurrencyCode, string? SupplierReference, DateTime? BillDate, DateTime? DueDate,
    string? Notes, IReadOnlyList<SaveTravelSupplierBillLineDto> Lines);
public record UpdateTravelSupplierBillDto(string? SupplierReference, DateTime? BillDate, DateTime? DueDate, string? Notes,
    IReadOnlyList<SaveTravelSupplierBillLineDto> Lines);
public record PostTravelSupplierBillDto(DateTime? BillDate, DateTime? DueDate);
public record RecordTravelSupplierPaymentDto(Guid IdempotencyKey, int SupplierId, DateTime PaidOn, string Method, string? Reference, decimal Amount,
    string CurrencyCode, bool Cleared, string? Notes, IReadOnlyList<TravelAllocationInputDto> Allocations);
public record IssueTravelSupplierCreditDto(Guid IdempotencyKey, int BillId, decimal Amount, string Reason, int? CancellationId, DateTime? IssuedOn);
public record RecordTravelSupplierRefundDto(Guid IdempotencyKey, int? BillId, int? PaymentId, decimal Amount, string Method, string? Reference,
    DateTime ReceivedOn, int? CancellationId, string? Notes);

// ── Internal facts ──────────────────────────────────────────────────────

public sealed class TravelSupplierBillState : TravelInvoiceMoney
{
    public int Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public int SupplierId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime? BillDate { get; init; }
    public DateTime? DueDate { get; init; }
    public int? BranchId { get; init; }
    public int AllocationCount { get; init; }
    public int CreditCount { get; init; }
    public int RefundCount { get; init; }
}

public sealed class TravelSupplierPaymentState
{
    public int Id { get; init; }
    public int SupplierId { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Allocated { get; init; }
    public decimal Refunded { get; init; }
    public DateTime? ReconciledAt { get; init; }
    public string Method { get; init; } = string.Empty;
    public decimal Unallocated => Amount - Allocated - Refunded;
}

public sealed record TravelSupplierBillHeaderWrite(int SupplierId, string CurrencyCode, string? SupplierReference, DateTime? BillDate, DateTime? DueDate,
    decimal Gross, decimal CommissionTotal, decimal Net, string? Notes, int? BranchId);

public sealed record TravelSupplierBillLineWrite(int Sequence, string Description, string? SourceType, int? SourceId, string? ContextType, int? ContextId,
    decimal Amount, decimal CommissionAmount, decimal NetAmount);

public sealed record TravelSupplierBillListFilter(string? Search, string? Status, int? SupplierId, string? CurrencyCode, DateTime? BillFrom, DateTime? BillTo,
    bool OverdueOnly, DateTime Today);

public sealed record TravelSupplierPaymentListFilter(string? Search, string? Status, int? SupplierId, DateTime? PaidFrom, DateTime? PaidTo, bool? Reconciled);
