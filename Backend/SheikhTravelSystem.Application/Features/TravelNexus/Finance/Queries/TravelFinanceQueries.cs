using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Finance.Queries;

internal static class FinanceRange
{
    /// <summary>An inverted range is empty by definition — never fall back to unfiltered data.</summary>
    public static bool IsInverted(DateTime? from, DateTime? to) => from.HasValue && to.HasValue && to.Value.Date < from.Value.Date;

    public static PagedResult<T> Empty<T>(int page, int pageSize) => new() { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
}

// ── Invoices ────────────────────────────────────────────────────────────

public class GetTravelInvoicesQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelInvoiceListItemDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? PaymentStatus { get; set; }
    public int? CustomerId { get; set; }
    public string? CurrencyCode { get; set; }
    public DateTime? IssueFrom { get; set; }
    public DateTime? IssueTo { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public bool? Overdue { get; set; }
}

public class GetTravelInvoicesQueryValidator : AbstractValidator<GetTravelInvoicesQuery>
{
    public GetTravelInvoicesQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.InvoiceStatuses)).When(x => !string.IsNullOrEmpty(x.Status));
        RuleFor(x => x.PaymentStatus).Must(s => TravelValues.IsOneOf(s, TravelValues.InvoicePaymentStatuses)).When(x => !string.IsNullOrEmpty(x.PaymentStatus));
        RuleFor(x => x.CurrencyCode).Must(TravelValues.IsCurrencyCode).When(x => !string.IsNullOrEmpty(x.CurrencyCode));
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class GetTravelInvoicesQueryHandler(ITravelInvoiceRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelInvoicesQuery, ApiResponse<PagedResult<TravelInvoiceListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelInvoiceListItemDto>>> Handle(GetTravelInvoicesQuery request, CancellationToken cancellationToken)
    {
        if (FinanceRange.IsInverted(request.IssueFrom, request.IssueTo) || FinanceRange.IsInverted(request.DueFrom, request.DueTo))
            return ApiResponse<PagedResult<TravelInvoiceListItemDto>>.SuccessResponse(FinanceRange.Empty<TravelInvoiceListItemDto>(request.Page, request.PageSize));
        var tenantId = tenantContext.GetRequiredTenantId();
        var today = DateTime.UtcNow.Date;
        var filter = new TravelInvoiceListFilter(string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrEmpty(request.Status) ? null : request.Status, string.IsNullOrEmpty(request.PaymentStatus) ? null : request.PaymentStatus,
            request.CustomerId, string.IsNullOrEmpty(request.CurrencyCode) ? null : request.CurrencyCode.ToUpperInvariant(),
            request.IssueFrom?.Date, request.IssueTo?.Date, request.DueFrom?.Date, request.DueTo?.Date, request.Overdue == true, today);
        var page = await repository.GetPagedAsync(tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), filter,
            request.Page, request.PageSize, cancellationToken);
        var installments = (await repository.GetInstallmentsAsync(page.Items.Select(i => i.Id).ToList(), tenantId, cancellationToken))
            .GroupBy(i => i.InvoiceId).ToDictionary(g => g.Key, g => (IReadOnlyList<TravelInstallmentDto>)g.ToList());
        foreach (var i in page.Items) TravelFinanceRules.Decorate(i, installments.GetValueOrDefault(i.Id, []), today);
        return ApiResponse<PagedResult<TravelInvoiceListItemDto>>.SuccessResponse(page);
    }
}

public record GetTravelInvoiceByIdQuery(int Id) : IRequest<ApiResponse<TravelInvoiceDto>>;

public class GetTravelInvoiceByIdQueryHandler(ITravelInvoiceRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext)
    : IRequestHandler<GetTravelInvoiceByIdQuery, ApiResponse<TravelInvoiceDto>>
{
    public async Task<ApiResponse<TravelInvoiceDto>> Handle(GetTravelInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetInvoiceAsync(request.Id, tenantId, cancellationToken);
        var invoice = await repository.GetDetailAsync(state.Id, tenantId, cancellationToken) ?? throw new NotFoundException("TravelInvoice", request.Id);
        TravelFinanceRules.Decorate(invoice, invoice.Installments, DateTime.UtcNow.Date);
        invoice.AllowedActions = TravelInvoiceStateMachine.AllowedActions(state);
        return ApiResponse<TravelInvoiceDto>.SuccessResponse(invoice);
    }
}

/// <summary>Uninvoiced sold items of a customer (optionally one currency); with <c>InvoiceId</c>, also the items already on that draft.</summary>
public record GetTravelBillableItemsQuery(int CustomerId, string? CurrencyCode, int? InvoiceId) : IRequest<ApiResponse<IReadOnlyList<TravelBillableItemDto>>>;

public class GetTravelBillableItemsQueryHandler(ITravelInvoiceRepository repository, ITravelCustomerRepository customers, TravelFinanceGuard guard,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelBillableItemsQuery, ApiResponse<IReadOnlyList<TravelBillableItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelBillableItemDto>>> Handle(GetTravelBillableItemsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        _ = await customers.GetByIdAsync(request.CustomerId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
            ?? throw new NotFoundException("TravelCustomer", request.CustomerId);
        if (request.InvoiceId is int invoiceId)
        {
            var invoice = await guard.GetInvoiceAsync(invoiceId, tenantId, cancellationToken);
            if (invoice.CustomerId != request.CustomerId) throw new NotFoundException("TravelInvoice", invoiceId);
        }
        var currency = TravelValues.IsCurrencyCode(request.CurrencyCode?.ToUpperInvariant()) ? request.CurrencyCode!.ToUpperInvariant() : null;
        return ApiResponse<IReadOnlyList<TravelBillableItemDto>>.SuccessResponse(
            await repository.GetBillableAsync(tenantId, TravelBranchScope.CompanyWide, request.CustomerId, currency, request.InvoiceId, cancellationToken));
    }
}

/// <summary>Issued invoices of a customer with something left to pay (allocation dialog), oldest due first.</summary>
public record GetTravelOpenInvoicesQuery(int CustomerId) : IRequest<ApiResponse<IReadOnlyList<TravelInvoiceListItemDto>>>;

public class GetTravelOpenInvoicesQueryHandler(ITravelInvoiceRepository repository, ITravelCustomerRepository customers,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelOpenInvoicesQuery, ApiResponse<IReadOnlyList<TravelInvoiceListItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelInvoiceListItemDto>>> Handle(GetTravelOpenInvoicesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        _ = await customers.GetByIdAsync(request.CustomerId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
            ?? throw new NotFoundException("TravelCustomer", request.CustomerId);
        var open = await repository.GetOpenInvoicesAsync(tenantId, request.CustomerId, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var installments = (await repository.GetInstallmentsAsync(open.Select(i => i.Id).ToList(), tenantId, cancellationToken))
            .GroupBy(i => i.InvoiceId).ToDictionary(g => g.Key, g => (IReadOnlyList<TravelInstallmentDto>)g.ToList());
        foreach (var i in open) TravelFinanceRules.Decorate(i, installments.GetValueOrDefault(i.Id, []), today);
        return ApiResponse<IReadOnlyList<TravelInvoiceListItemDto>>.SuccessResponse(open);
    }
}

// ── Payments, credit notes, refunds ─────────────────────────────────────

public class GetTravelPaymentsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelPaymentListItemDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Method { get; set; }
    public int? CustomerId { get; set; }
    public DateTime? ReceivedFrom { get; set; }
    public DateTime? ReceivedTo { get; set; }
    public bool? Reconciled { get; set; }
}

public class GetTravelPaymentsQueryValidator : AbstractValidator<GetTravelPaymentsQuery>
{
    public GetTravelPaymentsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.PaymentStatuses)).When(x => !string.IsNullOrEmpty(x.Status));
        RuleFor(x => x.Method).Must(m => TravelValues.IsOneOf(m, TravelValues.PaymentMethods)).When(x => !string.IsNullOrEmpty(x.Method));
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class GetTravelPaymentsQueryHandler(ITravelPaymentRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelPaymentsQuery, ApiResponse<PagedResult<TravelPaymentListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelPaymentListItemDto>>> Handle(GetTravelPaymentsQuery request, CancellationToken cancellationToken)
    {
        if (FinanceRange.IsInverted(request.ReceivedFrom, request.ReceivedTo))
            return ApiResponse<PagedResult<TravelPaymentListItemDto>>.SuccessResponse(FinanceRange.Empty<TravelPaymentListItemDto>(request.Page, request.PageSize));
        var tenantId = tenantContext.GetRequiredTenantId();
        var filter = new TravelPaymentListFilter(string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrEmpty(request.Status) ? null : request.Status, string.IsNullOrEmpty(request.Method) ? null : request.Method, request.CustomerId,
            request.ReceivedFrom?.Date, request.ReceivedTo?.Date, request.Reconciled);
        return ApiResponse<PagedResult<TravelPaymentListItemDto>>.SuccessResponse(await repository.GetPagedAsync(tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), filter, request.Page, request.PageSize, cancellationToken));
    }
}

public record GetTravelPaymentByIdQuery(int Id) : IRequest<ApiResponse<TravelPaymentDto>>;

public class GetTravelPaymentByIdQueryHandler(ITravelPaymentRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext)
    : IRequestHandler<GetTravelPaymentByIdQuery, ApiResponse<TravelPaymentDto>>
{
    public async Task<ApiResponse<TravelPaymentDto>> Handle(GetTravelPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetPaymentAsync(request.Id, tenantId, cancellationToken);
        var payment = await repository.GetDetailAsync(state.Id, tenantId, cancellationToken) ?? throw new NotFoundException("TravelPayment", request.Id);
        payment.AllowedActions = TravelFinanceRules.PaymentActions(state);
        return ApiResponse<TravelPaymentDto>.SuccessResponse(payment);
    }
}

public class GetTravelCreditNotesQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelCreditNoteDto>>>
{
    public int? CustomerId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class GetTravelCreditNotesQueryHandler(ITravelPaymentRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelCreditNotesQuery, ApiResponse<PagedResult<TravelCreditNoteDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelCreditNoteDto>>> Handle(GetTravelCreditNotesQuery request, CancellationToken cancellationToken)
    {
        if (FinanceRange.IsInverted(request.From, request.To))
            return ApiResponse<PagedResult<TravelCreditNoteDto>>.SuccessResponse(FinanceRange.Empty<TravelCreditNoteDto>(request.Page, request.PageSize));
        var tenantId = tenantContext.GetRequiredTenantId();
        return ApiResponse<PagedResult<TravelCreditNoteDto>>.SuccessResponse(await repository.GetCreditNotesAsync(tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), request.CustomerId, request.From?.Date, request.To?.Date,
            request.Page, request.PageSize, cancellationToken));
    }
}

public class GetTravelRefundsQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelRefundDto>>>
{
    public int? CustomerId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class GetTravelRefundsQueryHandler(ITravelPaymentRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelRefundsQuery, ApiResponse<PagedResult<TravelRefundDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelRefundDto>>> Handle(GetTravelRefundsQuery request, CancellationToken cancellationToken)
    {
        if (FinanceRange.IsInverted(request.From, request.To))
            return ApiResponse<PagedResult<TravelRefundDto>>.SuccessResponse(FinanceRange.Empty<TravelRefundDto>(request.Page, request.PageSize));
        var tenantId = tenantContext.GetRequiredTenantId();
        return ApiResponse<PagedResult<TravelRefundDto>>.SuccessResponse(await repository.GetRefundsAsync(tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), request.CustomerId, request.From?.Date, request.To?.Date,
            request.Page, request.PageSize, cancellationToken));
    }
}

// ── Reports ─────────────────────────────────────────────────────────────

public record GetTravelFinanceSummaryQuery : IRequest<ApiResponse<TravelFinanceSummaryDto>>;

public class GetTravelFinanceSummaryQueryHandler(ITravelInvoiceRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelFinanceSummaryQuery, ApiResponse<TravelFinanceSummaryDto>>
{
    public async Task<ApiResponse<TravelFinanceSummaryDto>> Handle(GetTravelFinanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var currencies = await repository.GetCurrencySummaryAsync(tenantId, scope, today, new DateTime(today.Year, today.Month, 1), cancellationToken);
        var counts = await repository.GetStatusCountsAsync(tenantId, scope, cancellationToken);
        return ApiResponse<TravelFinanceSummaryDto>.SuccessResponse(Build(currencies, counts));
    }

    public static TravelFinanceSummaryDto Build(IReadOnlyList<TravelFinanceCurrencySummaryDto> currencies, IReadOnlyDictionary<string, int> counts)
    {
        var filled = TravelValues.InvoiceStatuses.ToDictionary(s => s, s => counts.TryGetValue(s, out var n) ? n : 0);
        return new TravelFinanceSummaryDto
        {
            Currencies = currencies.OrderBy(c => c.CurrencyCode, StringComparer.Ordinal).ToList(),
            InvoiceCounts = filled,
            InvoiceTotal = filled.Values.Sum()
        };
    }
}

/// <summary>Balances per customer and currency (issued invoices; advances from cleared payments).</summary>
public record GetTravelCustomerBalancesQuery(int? CustomerId, bool OpenOnly = true) : IRequest<ApiResponse<IReadOnlyList<TravelCustomerBalanceDto>>>;

public class GetTravelCustomerBalancesQueryHandler(ITravelInvoiceRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelCustomerBalancesQuery, ApiResponse<IReadOnlyList<TravelCustomerBalanceDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelCustomerBalanceDto>>> Handle(GetTravelCustomerBalancesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return ApiResponse<IReadOnlyList<TravelCustomerBalanceDto>>.SuccessResponse(await repository.GetCustomerBalancesAsync(tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), request.CustomerId, DateTime.UtcNow.Date, request.OpenOnly, cancellationToken));
    }
}

public record GetTravelCustomerStatementQuery(int CustomerId, string? CurrencyCode, DateTime? From, DateTime? To) : IRequest<ApiResponse<TravelStatementDto>>;

public class GetTravelCustomerStatementQueryHandler(ITravelInvoiceRepository repository, ITravelCustomerRepository customers,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext) : IRequestHandler<GetTravelCustomerStatementQuery, ApiResponse<TravelStatementDto>>
{
    public async Task<ApiResponse<TravelStatementDto>> Handle(GetTravelCustomerStatementQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var customer = await customers.GetByIdAsync(request.CustomerId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
                       ?? throw new NotFoundException("TravelCustomer", request.CustomerId);
        var currencies = await repository.GetCustomerCurrenciesAsync(tenantId, customer.CustomerId, cancellationToken);
        var currency = request.CurrencyCode?.ToUpperInvariant() is { } c && currencies.Contains(c) ? c : currencies.FirstOrDefault();
        var dto = new TravelStatementDto
        {
            CustomerId = customer.CustomerId, CustomerName = customer.FullName, CurrencyCode = currency ?? string.Empty,
            From = request.From?.Date, To = request.To?.Date, Currencies = currencies
        };
        if (currency is null) return ApiResponse<TravelStatementDto>.SuccessResponse(dto);
        var movements = await repository.GetStatementMovementsAsync(tenantId, customer.CustomerId, currency, cancellationToken);
        var (opening, entries, closing) = TravelFinanceRules.Statement(movements, dto.From, dto.To);
        dto.OpeningBalance = opening;
        dto.Entries = entries;
        dto.ClosingBalance = closing;
        return ApiResponse<TravelStatementDto>.SuccessResponse(dto);
    }
}

/// <summary>Reconciliation: sold items with a price that sit on no live invoice (tenant-wide, newest first, capped).</summary>
public record GetTravelUninvoicedQuery : IRequest<ApiResponse<IReadOnlyList<TravelBillableItemDto>>>;

public class GetTravelUninvoicedQueryHandler(ITravelInvoiceRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelUninvoicedQuery, ApiResponse<IReadOnlyList<TravelBillableItemDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelBillableItemDto>>> Handle(GetTravelUninvoicedQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return ApiResponse<IReadOnlyList<TravelBillableItemDto>>.SuccessResponse(await repository.GetBillableAsync(tenantId,
            await scopeResolver.ResolveAsync(tenantId, cancellationToken), null, null, null, cancellationToken));
    }
}

/// <summary>Reconciliation: issued invoice lines whose source amount changed (or was cancelled) after invoicing.</summary>
public record GetTravelChangedInvoiceLinesQuery : IRequest<ApiResponse<IReadOnlyList<TravelChangedInvoiceLineDto>>>;

public class GetTravelChangedInvoiceLinesQueryHandler(ITravelInvoiceRepository repository, ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext)
    : IRequestHandler<GetTravelChangedInvoiceLinesQuery, ApiResponse<IReadOnlyList<TravelChangedInvoiceLineDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelChangedInvoiceLineDto>>> Handle(GetTravelChangedInvoiceLinesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return ApiResponse<IReadOnlyList<TravelChangedInvoiceLineDto>>.SuccessResponse(
            await repository.GetChangedLinesAsync(tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken));
    }
}

/// <summary>Group finance: per booking party, pilgrims quoted vs invoiced, paid and balance.</summary>
public record GetTravelGroupFinanceQuery(int GroupId) : IRequest<ApiResponse<IReadOnlyList<TravelGroupFinanceRowDto>>>;

public class GetTravelGroupFinanceQueryHandler(ITravelInvoiceRepository repository, ITravelGroupRepository groups, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext) : IRequestHandler<GetTravelGroupFinanceQuery, ApiResponse<IReadOnlyList<TravelGroupFinanceRowDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelGroupFinanceRowDto>>> Handle(GetTravelGroupFinanceQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        _ = await groups.GetStateAsync(request.GroupId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
            ?? throw new NotFoundException("TravelGroup", request.GroupId);
        return ApiResponse<IReadOnlyList<TravelGroupFinanceRowDto>>.SuccessResponse(await repository.GetGroupFinanceAsync(tenantId, request.GroupId, cancellationToken));
    }
}
