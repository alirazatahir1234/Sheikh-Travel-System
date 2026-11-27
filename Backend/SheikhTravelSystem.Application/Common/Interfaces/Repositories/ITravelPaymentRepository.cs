using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Customer payments (receipts), allocations, credit notes and refunds. Every statement filters on TenantId. Money writes run in
/// one transaction that locks the affected invoices / payments and re-checks every cap before writing.
/// </summary>
public interface ITravelPaymentRepository
{
    Task<TravelPaymentState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<PagedResult<TravelPaymentListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelPaymentListFilter filter, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<TravelPaymentDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Records the payment and its allocations; replays the original when the idempotency key was seen before.</summary>
    /// <summary>
    /// Id of the receipt, credit note or customer refund already written under <paramref name="key"/>, or null. Handlers
    /// check this before validating so a retry returns the original record instead of failing against the changed balance.
    /// </summary>
    Task<int?> FindByIdempotencyKeyAsync(int tenantId, TravelIdempotentRecord kind, Guid key, CancellationToken cancellationToken = default);
    Task<TravelFinanceWriteResult> RecordAsync(int tenantId, TravelPaymentWrite payment, IReadOnlyList<TravelAllocationWrite> allocations, int? userId,
        DateTime utcNow, CancellationToken cancellationToken = default);
    Task<TravelFinanceWriteResult> AllocateAsync(int tenantId, int paymentId, IReadOnlyList<TravelAllocationWrite> allocations, int? userId,
        CancellationToken cancellationToken = default);
    Task<bool> UnallocateAsync(int tenantId, int paymentId, int invoiceId, int? userId, CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(int tenantId, int paymentId, string expectedStatus, string newStatus, string? note, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task<bool> ReconcileAsync(int tenantId, int paymentId, string bankReference, string? note, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<TravelCreditNoteState?> GetCreditNoteAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<TravelCreditNoteDto>> GetCreditNotesAsync(int tenantId, TravelBranchScope scope, int? customerId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<TravelFinanceWriteResult> IssueCreditNoteAsync(int tenantId, Guid idempotencyKey, int invoiceId, decimal amount, string reason, int? cancellationId,
        DateTime issuedOn, int? userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<bool> VoidCreditNoteAsync(int tenantId, int creditNoteId, string reason, int? userId, CancellationToken cancellationToken = default);

    Task<PagedResult<TravelRefundDto>> GetRefundsAsync(int tenantId, TravelBranchScope scope, int? customerId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Records a refund (caps re-checked under lock); a linked cancellation moves to PaidToCustomer in the same transaction.</summary>
    Task<TravelFinanceWriteResult> RecordRefundAsync(int tenantId, RecordTravelRefundDto refund, int customerId, string currencyCode, int? userId,
        DateTime utcNow, bool advanceCancellation, CancellationToken cancellationToken = default);

    Task<TravelCancellationFacts?> GetCancellationFactsAsync(int tenantId, int cancellationId, CancellationToken cancellationToken = default);
}
