using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Queries;

internal static class SupplierRange
{
    /// <summary>An inverted range is empty by definition — never fall back to unfiltered data.</summary>
    public static bool IsInverted(DateTime? from, DateTime? to) => from.HasValue && to.HasValue && to.Value.Date < from.Value.Date;

    public static PagedResult<T> Empty<T>(int page, int pageSize) => new() { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };

    public static void Decorate(TravelSupplierBillListItemDto bill, DateTime today)
    {
        bill.PaymentStatus = TravelSupplierFinanceRules.PaymentStatus(bill.Status, bill);
        bill.Overdue = bill.Status == "Posted" && bill.Balance > 0 && bill.DueDate is DateTime due && due.Date < today;
    }
}

// ── Bills ───────────────────────────────────────────────────────────────

public class GetTravelSupplierBillsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelSupplierBillListItemDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? SupplierId { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime? BillFrom { get; set; }
    public DateTime? BillTo { get; set; }
    public bool? Overdue { get; set; }
}

public class GetTravelSupplierBillsQueryValidator : AbstractValidator<GetTravelSupplierBillsQuery>
{
    public GetTravelSupplierBillsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.SupplierBillStatuses)).When(x => !string.IsNullOrEmpty(x.Status));
        RuleFor(x => x.CurrencyCode).Must(TravelValues.IsCurrencyCode).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class GetTravelSupplierBillsQueryHandler(ITravelSupplierBillRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierBillsQuery, ApiResponse<PagedResult<TravelSupplierBillListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelSupplierBillListItemDto>>> Handle(GetTravelSupplierBillsQuery request, CancellationToken cancellationToken)
    {
        if (SupplierRange.IsInverted(request.BillFrom, request.BillTo))
            return ApiResponse<PagedResult<TravelSupplierBillListItemDto>>.SuccessResponse(SupplierRange.Empty<TravelSupplierBillListItemDto>(request.Page, request.PageSize));
        var tenantId = tenantContext.GetRequiredTenantId();
        var today = DateTime.UtcNow.Date;
        var filter = new TravelSupplierBillListFilter(string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrEmpty(request.Status) ? null : request.Status, request.SupplierId,
            string.IsNullOrEmpty(request.CurrencyCode) ? null : request.CurrencyCode.ToUpperInvariant(), request.BillFrom?.Date, request.BillTo?.Date,
            request.Overdue == true, today);
        var page = await repository.GetPagedAsync(tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), filter, request.Page, request.PageSize,
            cancellationToken);
        foreach (var b in page.Items) SupplierRange.Decorate(b, today);
        return ApiResponse<PagedResult<TravelSupplierBillListItemDto>>.SuccessResponse(page);
    }
}

public record GetTravelSupplierBillByIdQuery(int Id) : IRequest<ApiResponse<TravelSupplierBillDto>>;

public class GetTravelSupplierBillByIdQueryHandler(ITravelSupplierBillRepository repository, TravelSupplierFinanceGuard guard, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierBillByIdQuery, ApiResponse<TravelSupplierBillDto>>
{
    public async Task<ApiResponse<TravelSupplierBillDto>> Handle(GetTravelSupplierBillByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetBillAsync(request.Id, tenantId, cancellationToken);
        var bill = await repository.GetDetailAsync(state.Id, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplierBill", request.Id);
        SupplierRange.Decorate(bill, DateTime.UtcNow.Date);
        bill.AllowedActions = TravelSupplierBillStateMachine.AllowedActions(state);
        return ApiResponse<TravelSupplierBillDto>.SuccessResponse(bill);
    }
}

/// <summary>A supplier's unbilled services (optionally one currency) with the commission its rules suggest on <c>BillDate</c>.</summary>
public record GetTravelSupplierBillableQuery(int SupplierId, string? CurrencyCode, int? BillId, DateTime? BillDate)
    : IRequest<ApiResponse<IReadOnlyList<TravelBillableCostDto>>>;

public class GetTravelSupplierBillableQueryHandler(ITravelSupplierBillRepository repository, ITravelSupplierRepository suppliers, ITravelSupplierSetupRepository setup,
    TravelSupplierFinanceGuard guard, ITenantContext tenantContext) : IRequestHandler<GetTravelSupplierBillableQuery, ApiResponse<IReadOnlyList<TravelBillableCostDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelBillableCostDto>>> Handle(GetTravelSupplierBillableQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        _ = await suppliers.GetByIdAsync(request.SupplierId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplier", request.SupplierId);
        if (request.BillId is int billId)
        {
            var bill = await guard.GetBillAsync(billId, tenantId, cancellationToken);
            if (bill.SupplierId != request.SupplierId) throw new NotFoundException("TravelSupplierBill", billId);
        }
        var currency = TravelValues.IsCurrencyCode(request.CurrencyCode?.ToUpperInvariant()) ? request.CurrencyCode!.ToUpperInvariant() : null;
        var items = await repository.GetBillableCostsAsync(tenantId, request.SupplierId, currency, request.BillId, cancellationToken);
        var rules = await setup.GetCommissionRulesAsync(tenantId, request.SupplierId, cancellationToken);
        var on = (request.BillDate ?? DateTime.UtcNow).Date;
        foreach (var i in items)
            i.SuggestedCommission = TravelSupplierFinanceRules.SuggestCommission(rules, i.SourceType, i.Amount, i.Units, i.CurrencyCode, on);
        return ApiResponse<IReadOnlyList<TravelBillableCostDto>>.SuccessResponse(items);
    }
}

/// <summary>Posted bills of a supplier with something left to pay (allocation dialog), oldest due first.</summary>
public record GetTravelSupplierOpenBillsQuery(int SupplierId) : IRequest<ApiResponse<IReadOnlyList<TravelSupplierBillListItemDto>>>;

public class GetTravelSupplierOpenBillsQueryHandler(ITravelSupplierBillRepository repository, ITravelSupplierRepository suppliers, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierOpenBillsQuery, ApiResponse<IReadOnlyList<TravelSupplierBillListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelSupplierBillListItemDto>>> Handle(GetTravelSupplierOpenBillsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        _ = await suppliers.GetByIdAsync(request.SupplierId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplier", request.SupplierId);
        var open = await repository.GetOpenBillsAsync(tenantId, request.SupplierId, cancellationToken);
        var today = DateTime.UtcNow.Date;
        foreach (var b in open) SupplierRange.Decorate(b, today);
        return ApiResponse<IReadOnlyList<TravelSupplierBillListItemDto>>.SuccessResponse(open);
    }
}

// ── Payments, credits, refunds ──────────────────────────────────────────

public class GetTravelSupplierPaymentsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelSupplierPaymentListItemDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? PaidFrom { get; set; }
    public DateTime? PaidTo { get; set; }
    public bool? Reconciled { get; set; }
}

public class GetTravelSupplierPaymentsQueryValidator : AbstractValidator<GetTravelSupplierPaymentsQuery>
{
    public GetTravelSupplierPaymentsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.PaymentStatuses)).When(x => !string.IsNullOrEmpty(x.Status));
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class GetTravelSupplierPaymentsQueryHandler(ITravelSupplierPaymentRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierPaymentsQuery, ApiResponse<PagedResult<TravelSupplierPaymentListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelSupplierPaymentListItemDto>>> Handle(GetTravelSupplierPaymentsQuery request, CancellationToken cancellationToken)
    {
        if (SupplierRange.IsInverted(request.PaidFrom, request.PaidTo))
            return ApiResponse<PagedResult<TravelSupplierPaymentListItemDto>>.SuccessResponse(
                SupplierRange.Empty<TravelSupplierPaymentListItemDto>(request.Page, request.PageSize));
        var tenantId = tenantContext.GetRequiredTenantId();
        var filter = new TravelSupplierPaymentListFilter(string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrEmpty(request.Status) ? null : request.Status, request.SupplierId, request.PaidFrom?.Date, request.PaidTo?.Date, request.Reconciled);
        return ApiResponse<PagedResult<TravelSupplierPaymentListItemDto>>.SuccessResponse(await repository.GetPagedAsync(tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), filter, request.Page, request.PageSize, cancellationToken));
    }
}

public record GetTravelSupplierPaymentByIdQuery(int Id) : IRequest<ApiResponse<TravelSupplierPaymentDto>>;

public class GetTravelSupplierPaymentByIdQueryHandler(ITravelSupplierPaymentRepository repository, TravelSupplierFinanceGuard guard, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierPaymentByIdQuery, ApiResponse<TravelSupplierPaymentDto>>
{
    public async Task<ApiResponse<TravelSupplierPaymentDto>> Handle(GetTravelSupplierPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var s = await guard.GetPaymentAsync(request.Id, tenantId, cancellationToken);
        var payment = await repository.GetDetailAsync(s.Id, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplierPayment", request.Id);
        payment.AllowedActions = TravelSupplierFinanceRules.PaymentActions(s.Status, s.Amount, s.Allocated, s.Refunded, s.ReconciledAt, s.Method);
        return ApiResponse<TravelSupplierPaymentDto>.SuccessResponse(payment);
    }
}

public class GetTravelSupplierCreditsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelSupplierCreditDto>>>
{
    public int? SupplierId { get; set; }
}

public class GetTravelSupplierCreditsQueryHandler(ITravelSupplierPaymentRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierCreditsQuery, ApiResponse<PagedResult<TravelSupplierCreditDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelSupplierCreditDto>>> Handle(GetTravelSupplierCreditsQuery request, CancellationToken cancellationToken)
        => ApiResponse<PagedResult<TravelSupplierCreditDto>>.SuccessResponse(await repository.GetCreditsAsync(tenantContext.GetRequiredTenantId(),
            request.SupplierId, request.Page, request.PageSize, cancellationToken));
}

public class GetTravelSupplierRefundsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelSupplierRefundDto>>>
{
    public int? SupplierId { get; set; }
}

public class GetTravelSupplierRefundsQueryHandler(ITravelSupplierPaymentRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierRefundsQuery, ApiResponse<PagedResult<TravelSupplierRefundDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelSupplierRefundDto>>> Handle(GetTravelSupplierRefundsQuery request, CancellationToken cancellationToken)
        => ApiResponse<PagedResult<TravelSupplierRefundDto>>.SuccessResponse(await repository.GetRefundsAsync(tenantContext.GetRequiredTenantId(),
            request.SupplierId, request.Page, request.PageSize, cancellationToken));
}

// ── Payables reports ────────────────────────────────────────────────────

public record GetTravelPayablesSummaryQuery : IRequest<ApiResponse<TravelPayablesSummaryDto>>;

public class GetTravelPayablesSummaryQueryHandler(ITravelSupplierBillRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelPayablesSummaryQuery, ApiResponse<TravelPayablesSummaryDto>>
{
    public async Task<ApiResponse<TravelPayablesSummaryDto>> Handle(GetTravelPayablesSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var currencies = await repository.GetCurrencySummaryAsync(tenantId, scope, today, new DateTime(today.Year, today.Month, 1), cancellationToken);
        var counts = await repository.GetStatusCountsAsync(tenantId, scope, cancellationToken);
        return ApiResponse<TravelPayablesSummaryDto>.SuccessResponse(Build(currencies, counts));
    }

    public static TravelPayablesSummaryDto Build(IReadOnlyList<TravelPayablesCurrencySummaryDto> currencies, IReadOnlyDictionary<string, int> counts)
    {
        var filled = TravelValues.SupplierBillStatuses.ToDictionary(s => s, s => counts.TryGetValue(s, out var n) ? n : 0);
        return new TravelPayablesSummaryDto
        {
            Currencies = currencies.OrderBy(c => c.CurrencyCode, StringComparer.Ordinal).ToList(),
            BillCounts = filled,
            BillTotal = filled.Values.Sum()
        };
    }
}

/// <summary>Balances per supplier and currency (posted bills; advances from cleared payments).</summary>
public record GetTravelSupplierBalancesQuery(int? SupplierId, bool OpenOnly = true) : IRequest<ApiResponse<IReadOnlyList<TravelSupplierBalanceDto>>>;

public class GetTravelSupplierBalancesQueryHandler(ITravelSupplierBillRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierBalancesQuery, ApiResponse<IReadOnlyList<TravelSupplierBalanceDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelSupplierBalanceDto>>> Handle(GetTravelSupplierBalancesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return ApiResponse<IReadOnlyList<TravelSupplierBalanceDto>>.SuccessResponse(await repository.GetSupplierBalancesAsync(tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), request.SupplierId, DateTime.UtcNow.Date, request.OpenOnly, cancellationToken));
    }
}

public record GetTravelSupplierStatementQuery(int SupplierId, string? CurrencyCode, DateTime? From, DateTime? To) : IRequest<ApiResponse<TravelSupplierStatementDto>>;

public class GetTravelSupplierStatementQueryHandler(ITravelSupplierBillRepository repository, ITravelSupplierRepository suppliers, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSupplierStatementQuery, ApiResponse<TravelSupplierStatementDto>>
{
    public async Task<ApiResponse<TravelSupplierStatementDto>> Handle(GetTravelSupplierStatementQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var supplier = await suppliers.GetByIdAsync(request.SupplierId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplier", request.SupplierId);
        var currencies = await repository.GetSupplierCurrenciesAsync(tenantId, supplier.Id, cancellationToken);
        var currency = request.CurrencyCode?.ToUpperInvariant() is { } c && currencies.Contains(c) ? c : currencies.FirstOrDefault();
        var dto = new TravelSupplierStatementDto
        {
            SupplierId = supplier.Id, SupplierName = supplier.Name, CurrencyCode = currency ?? string.Empty, From = request.From?.Date, To = request.To?.Date,
            Currencies = currencies
        };
        if (currency is null || SupplierRange.IsInverted(dto.From, dto.To)) return ApiResponse<TravelSupplierStatementDto>.SuccessResponse(dto);
        var movements = await repository.GetStatementMovementsAsync(tenantId, supplier.Id, currency, cancellationToken);
        var (opening, entries, closing) = TravelFinanceRules.Statement(movements, dto.From, dto.To);
        dto.OpeningBalance = opening;
        dto.Entries = entries;
        dto.ClosingBalance = closing;
        return ApiResponse<TravelSupplierStatementDto>.SuccessResponse(dto);
    }
}
