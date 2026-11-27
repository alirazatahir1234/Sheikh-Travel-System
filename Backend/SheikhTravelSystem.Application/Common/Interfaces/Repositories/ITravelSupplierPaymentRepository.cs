using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Supplier payments, allocations, supplier credits and refunds received. Every statement filters on TenantId; money writes lock the
/// affected bill / payment and re-check caps in the same transaction. Idempotency keys make retries return the original row.
/// </summary>
public interface ITravelSupplierPaymentRepository
{
    Task<TravelSupplierPaymentState?> GetStateAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken cancellationToken = default);
    Task<PagedResult<TravelSupplierPaymentListItemDto>> GetPagedAsync(int tenantId, TravelBranchScope scope, TravelSupplierPaymentListFilter filter, int page,
        int pageSize, CancellationToken cancellationToken = default);
    Task<TravelSupplierPaymentDto?> GetDetailAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Id of the supplier payment, credit or refund already written under <paramref name="key"/>, or null (see ITravelPaymentRepository).</summary>
    Task<int?> FindByIdempotencyKeyAsync(int tenantId, TravelIdempotentRecord kind, Guid key, CancellationToken cancellationToken = default);
    Task<TravelFinanceWriteResult> RecordAsync(int tenantId, TravelPaymentWrite payment, IReadOnlyList<TravelAllocationWrite> allocations, int? userId,
        DateTime utcNow, CancellationToken cancellationToken = default);
    Task<TravelFinanceWriteResult> AllocateAsync(int tenantId, int paymentId, IReadOnlyList<TravelAllocationWrite> allocations, int? userId,
        CancellationToken cancellationToken = default);
    Task<bool> UnallocateAsync(int tenantId, int paymentId, int billId, int? userId, CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(int tenantId, int paymentId, string expectedStatus, string newStatus, string? note, int? userId, DateTime utcNow,
        CancellationToken cancellationToken = default);
    Task<bool> ReconcileAsync(int tenantId, int paymentId, string bankReference, string? note, int? userId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<TravelCreditNoteState?> GetCreditAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<TravelFinanceWriteResult> IssueCreditAsync(int tenantId, IssueTravelSupplierCreditDto credit, DateTime issuedOn, int? userId, DateTime utcNow,
        bool advanceCancellation, CancellationToken cancellationToken = default);
    Task<bool> VoidCreditAsync(int tenantId, int creditId, string reason, int? userId, CancellationToken cancellationToken = default);
    Task<TravelFinanceWriteResult> RecordRefundAsync(int tenantId, RecordTravelSupplierRefundDto refund, int supplierId, string currencyCode, int? userId,
        DateTime utcNow, bool advanceCancellation, CancellationToken cancellationToken = default);
    Task<PagedResult<TravelSupplierCreditDto>> GetCreditsAsync(int tenantId, int? supplierId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PagedResult<TravelSupplierRefundDto>> GetRefundsAsync(int tenantId, int? supplierId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>A Phase 5 cancellation with the supplier of the cancelled service and its refund tracking.</summary>
    Task<(int SupplierId, string RefundStatus, string? CurrencyCode)?> GetCancellationSupplierAsync(int tenantId, int cancellationId,
        CancellationToken cancellationToken = default);
}
