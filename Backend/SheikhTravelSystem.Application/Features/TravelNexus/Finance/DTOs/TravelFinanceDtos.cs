namespace SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

// ── Invoices ────────────────────────────────────────────────────────────

/// <summary>Money totals of one invoice. Balance = Total − Credited − (Paid − Refunded); negative = credit owed to the customer.</summary>
public class TravelInvoiceMoney
{
    public decimal Total { get; set; }
    /// <summary>Applied by Completed payments only.</summary>
    public decimal Paid { get; set; }
    /// <summary>Applied by payments still Pending (cheque / transfer not cleared) — reserved, not counted.</summary>
    public decimal PendingPaid { get; set; }
    public decimal Credited { get; set; }
    public decimal Refunded { get; set; }
    public decimal Balance => Total - Credited - (Paid - Refunded);
}

public class TravelInvoiceListItemDto : TravelInvoiceMoney
{
    public int Id { get; set; }
    /// <summary>Null while Draft.</summary>
    public string? InvoiceNumber { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime? IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    /// <summary>Draft | Issued | Void.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Unpaid | PartiallyPaid | Paid | CreditDue (issued invoices only).</summary>
    public string? PaymentStatus { get; set; }
    public DateTime? NextDueDate { get; set; }
    public decimal OverdueAmount { get; set; }
    public int? BookingId { get; set; }
    public string? BookingNumber { get; set; }
    public int? VisaCaseId { get; set; }
    public string? VisaCaseNumber { get; set; }
    public int? GroupId { get; set; }
    public string? GroupNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelInvoiceDto : TravelInvoiceListItemDto
{
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    /// <summary>Staff agent the invoice's commission is attributed to.</summary>
    public int? AgentUserId { get; set; }
    public string? AgentName { get; set; }
    /// <summary>Sub-agent the invoice's commission is attributed to.</summary>
    public int? SubAgentId { get; set; }
    public string? SubAgentName { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelInvoiceLineDto> Lines { get; set; } = [];
    public IReadOnlyList<TravelInstallmentDto> Installments { get; set; } = [];
    public IReadOnlyList<TravelInvoiceAllocationDto> Allocations { get; set; } = [];
    public IReadOnlyList<TravelCreditNoteDto> CreditNotes { get; set; } = [];
    public IReadOnlyList<TravelRefundDto> Refunds { get; set; } = [];
    public IReadOnlyList<TravelFinanceHistoryDto> History { get; set; } = [];
}

public class TravelInvoiceLineDto
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int Sequence { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    /// <summary>Flight | Hotel | VisaServiceFee | VisaEmbassyFee | GroupMember; null for a manual line.</summary>
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    /// <summary>The source's amount today (null when the source no longer has one, e.g. cancelled).</summary>
    public decimal? CurrentSourceAmount { get; set; }
    public bool SourceChanged => SourceType is not null && CurrentSourceAmount != LineTotal;
}

public class TravelInstallmentDto
{
    public int InvoiceId { get; set; }
    public int Sequence { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal Covered { get; set; }
    /// <summary>Paid | PartlyPaid | Due | Overdue.</summary>
    public string Status { get; set; } = string.Empty;
}

public class TravelInvoiceAllocationDto
{
    public int PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceivedOn { get; set; }
    public string Method { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public int InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string InvoiceCurrencyCode { get; set; } = string.Empty;
    public decimal AmountFromPayment { get; set; }
    public string PaymentCurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal AmountApplied { get; set; }
}

/// <summary>A sold item that can be put on an invoice.</summary>
public class TravelBillableItemDto
{
    /// <summary>Flight | Hotel | VisaServiceFee | VisaEmbassyFee | GroupMember.</summary>
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Booking | VisaCase | Group.</summary>
    public string ContextType { get; set; } = string.Empty;
    public int ContextId { get; set; }
    public string ContextLabel { get; set; } = string.Empty;
    public DateTime? ServiceDate { get; set; }
    /// <summary>Set when the item is already on the invoice being edited.</summary>
    public int? OnInvoiceId { get; set; }
}

// ── Payments, credit notes, refunds ─────────────────────────────────────

public class TravelPaymentListItemDto
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime ReceivedOn { get; set; }
    /// <summary>Cash | BankTransfer | Card | Cheque | Online | Other.</summary>
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Pending | Completed | Failed | Refunded.</summary>
    public string Status { get; set; } = string.Empty;
    public decimal Allocated { get; set; }
    public decimal Refunded { get; set; }
    public decimal Unallocated => Amount - Allocated - Refunded;
    public DateTime? ReconciledAt { get; set; }
    public string? BankReference { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TravelPaymentDto : TravelPaymentListItemDto
{
    public string? Notes { get; set; }
    public DateTime? ClearedAt { get; set; }
    public string? CreatedByName { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<TravelInvoiceAllocationDto> Allocations { get; set; } = [];
    public IReadOnlyList<TravelRefundDto> Refunds { get; set; } = [];
    public IReadOnlyList<TravelFinanceHistoryDto> History { get; set; } = [];
}

public class TravelCreditNoteDto
{
    public int Id { get; set; }
    public string CreditNoteNumber { get; set; } = string.Empty;
    public int InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? CancellationId { get; set; }
    /// <summary>Issued | Void.</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime IssuedOn { get; set; }
    public string? VoidReason { get; set; }
}

public class TravelRefundDto
{
    public int Id { get; set; }
    public string RefundNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public int? PaymentId { get; set; }
    public string? ReceiptNumber { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTime PaidOn { get; set; }
    public int? CancellationId { get; set; }
    public string? Notes { get; set; }
}

public class TravelFinanceHistoryDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Note { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

// ── Reports ─────────────────────────────────────────────────────────────

public class TravelFinanceCurrencySummaryDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Positive balances of issued invoices (all time).</summary>
    public decimal Outstanding { get; set; }
    /// <summary>Part of Outstanding past its due dates (as of today).</summary>
    public decimal Overdue { get; set; }
    /// <summary>Negative balances: credit owed back to customers (all time).</summary>
    public decimal CreditDue { get; set; }
    /// <summary>Completed payments received this calendar month (payment currency).</summary>
    public decimal CollectedThisMonth { get; set; }
    /// <summary>Completed payments not yet applied to invoices or refunded (payment currency).</summary>
    public decimal UnallocatedAdvances { get; set; }
}

public class TravelFinanceSummaryDto
{
    public IReadOnlyList<TravelFinanceCurrencySummaryDto> Currencies { get; set; } = [];
    public IReadOnlyDictionary<string, int> InvoiceCounts { get; set; } = new Dictionary<string, int>();
    public int InvoiceTotal { get; set; }
}

public class TravelCustomerBalanceDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public int OpenInvoices { get; set; }
    public decimal Invoiced { get; set; }
    public decimal Paid { get; set; }
    public decimal Credited { get; set; }
    public decimal Refunded { get; set; }
    public decimal Balance { get; set; }
    public decimal Overdue { get; set; }
    public decimal UnallocatedAdvances { get; set; }
}

public class TravelStatementEntryDto
{
    public DateTime Date { get; set; }
    /// <summary>Invoice | CreditNote | Payment | Refund | Advance.</summary>
    public string EntryType { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Increases what the customer owes.</summary>
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public int? InvoiceId { get; set; }
    public int? PaymentId { get; set; }
}

public class TravelStatementDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal OpeningBalance { get; set; }
    public IReadOnlyList<TravelStatementEntryDto> Entries { get; set; } = [];
    public decimal ClosingBalance { get; set; }
    /// <summary>Currencies the customer has movements in (for the currency switch).</summary>
    public IReadOnlyList<string> Currencies { get; set; } = [];
}

public class TravelChangedInvoiceLineDto
{
    public int InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public decimal Invoiced { get; set; }
    /// <summary>Null when the source no longer has an amount (e.g. cancelled).</summary>
    public decimal? Current { get; set; }
}

public class TravelGroupFinanceRowDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public int Pilgrims { get; set; }
    public decimal Quoted { get; set; }
    public int UninvoicedPilgrims { get; set; }
    public decimal Invoiced { get; set; }
    public decimal Paid { get; set; }
    public decimal Credited { get; set; }
    public decimal Balance { get; set; }
}

// ── Inputs ──────────────────────────────────────────────────────────────

public record SaveTravelInvoiceLineDto(string Description, decimal Quantity, decimal UnitPrice, string? SourceType, int? SourceId);
public record SaveTravelInstallmentDto(DateTime DueDate, decimal Amount);
/// <summary>Agent / sub-agent left null default to the file's assigned agent (else the customer's) and the customer's "referred by".</summary>
public record CreateTravelInvoiceDto(int CustomerId, string CurrencyCode, DateTime? IssueDate, DateTime? DueDate, decimal DiscountAmount,
    string? Notes, IReadOnlyList<SaveTravelInvoiceLineDto> Lines, IReadOnlyList<SaveTravelInstallmentDto> Installments,
    int? AgentUserId = null, int? SubAgentId = null);
/// <summary>Agent / sub-agent left null keep their current value (clearing goes through the attribution endpoint).</summary>
public record UpdateTravelInvoiceDto(DateTime? IssueDate, DateTime? DueDate, decimal DiscountAmount, string? Notes,
    IReadOnlyList<SaveTravelInvoiceLineDto> Lines, IReadOnlyList<SaveTravelInstallmentDto> Installments,
    int? AgentUserId = null, int? SubAgentId = null);
public record IssueTravelInvoiceDto(DateTime? IssueDate, DateTime? DueDate);
public record VoidTravelInvoiceDto(string Reason);

public record TravelAllocationInputDto(int InvoiceId, decimal AmountFromPayment, decimal? ExchangeRate);
public record RecordTravelPaymentDto(Guid IdempotencyKey, int CustomerId, DateTime ReceivedOn, string Method, string? Reference, decimal Amount,
    string CurrencyCode, bool Cleared, string? Notes, IReadOnlyList<TravelAllocationInputDto> Allocations);
public record AllocateTravelPaymentDto(IReadOnlyList<TravelAllocationInputDto> Allocations);
/// <summary>Completed (cleared) | Failed (bounced / reversed).</summary>
public record ChangeTravelPaymentStatusDto(string Status, string? Note);
public record ReconcileTravelPaymentDto(string BankReference, string? Note);

public record IssueTravelCreditNoteDto(Guid IdempotencyKey, int InvoiceId, decimal Amount, string Reason, int? CancellationId, DateTime? IssuedOn);
public record VoidTravelCreditNoteDto(string Reason);
/// <summary>Against an invoice's credit balance, or a payment's unallocated advance (exactly one).</summary>
public record RecordTravelRefundDto(Guid IdempotencyKey, int? InvoiceId, int? PaymentId, decimal Amount, string Method, string? Reference,
    DateTime PaidOn, int? CancellationId, string? Notes);

// ── Internal facts ──────────────────────────────────────────────────────

public sealed class TravelInvoiceState : TravelInvoiceMoney
{
    public int Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public int CustomerId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string? InvoiceNumber { get; init; }
    public DateTime? IssueDate { get; init; }
    public DateTime? DueDate { get; init; }
    public int? BranchId { get; init; }
    public int AllocationCount { get; init; }
    public int CreditNoteCount { get; init; }
    public int RefundCount { get; init; }
    public int InstallmentCount { get; init; }
    public decimal InstallmentTotal { get; init; }
    public DateTime? LastInstallmentDate { get; init; }
    public DateTime? FirstInstallmentDate { get; init; }
}

public sealed class TravelPaymentState
{
    public int Id { get; init; }
    public int CustomerId { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Allocated { get; init; }
    public decimal Refunded { get; init; }
    public DateTime? ReconciledAt { get; init; }
    public string Method { get; init; } = string.Empty;
    public decimal Unallocated => Amount - Allocated - Refunded;
}

public sealed record TravelCreditNoteState(int Id, int InvoiceId, string Status, decimal Amount);

/// <summary>A Phase 5 cancellation as seen by finance: who it belongs to and its refund tracking.</summary>
public sealed record TravelCancellationFacts(int Id, int BookingId, int? CustomerId, string RefundStatus, decimal RefundToCustomer, string? CurrencyCode);

public sealed record TravelInvoiceHeaderWrite(int CustomerId, string CurrencyCode, DateTime? IssueDate, DateTime? DueDate, decimal Subtotal,
    decimal DiscountAmount, decimal Total, string? Notes, int? BookingId, int? VisaCaseId, int? GroupId, int? BranchId,
    int? AgentUserId = null, int? SubAgentId = null);

public sealed record TravelInvoiceLineWrite(int Sequence, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal,
    string? SourceType, int? SourceId);

/// <summary>A validated allocation with the converted amount.</summary>
public sealed record TravelAllocationWrite(int InvoiceId, decimal AmountFromPayment, decimal ExchangeRate, decimal AmountApplied);

public sealed record TravelPaymentWrite(Guid IdempotencyKey, int CustomerId, DateTime ReceivedOn, string Method, string? Reference, decimal Amount,
    string CurrencyCode, string Status, string? Notes, int? BranchId);

/// <summary>Outcome of a money write that re-checks caps under lock: either an id, or why it was refused.</summary>
/// <summary>Records written under a client idempotency key (internal; never on the wire).</summary>
public enum TravelIdempotentRecord { Receipt, CreditNote, CustomerRefund, SupplierPayment, SupplierCredit, SupplierRefund }

public sealed record TravelFinanceWriteResult(int Id, string? Error, bool Replayed = false)
{
    public static TravelFinanceWriteResult Fail(string error) => new(0, error);
}

public sealed record TravelInvoiceListFilter(string? Search, string? Status, string? PaymentStatus, int? CustomerId, string? CurrencyCode,
    DateTime? IssueFrom, DateTime? IssueTo, DateTime? DueFrom, DateTime? DueTo, bool OverdueOnly, DateTime Today);

public sealed record TravelPaymentListFilter(string? Search, string? Status, string? Method, int? CustomerId, DateTime? ReceivedFrom,
    DateTime? ReceivedTo, bool? Reconciled);

/// <summary>Raw statement movement before running balances are computed.</summary>
public sealed record TravelStatementMovement(DateTime Date, string EntryType, string Reference, string Description, decimal Debit, decimal Credit,
    int? InvoiceId, int? PaymentId, int Order);
