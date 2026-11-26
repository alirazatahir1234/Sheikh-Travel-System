using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;

/// <summary>Visibility checks shared by finance commands and queries (other tenants / branches → NotFound).</summary>
public sealed class TravelFinanceGuard(ITravelInvoiceRepository invoices, ITravelPaymentRepository payments, ITravelBranchScopeResolver scopeResolver)
{
    public const string ConflictMessage = "The record was changed by someone else. Reload and try again.";

    public async Task<TravelInvoiceState> GetInvoiceAsync(int id, int tenantId, CancellationToken ct)
        => await invoices.GetStateAsync(id, tenantId, await scopeResolver.ResolveAsync(tenantId, ct), ct) ?? throw new NotFoundException("TravelInvoice", id);

    public async Task<TravelPaymentState> GetPaymentAsync(int id, int tenantId, CancellationToken ct)
        => await payments.GetStateAsync(id, tenantId, await scopeResolver.ResolveAsync(tenantId, ct), ct) ?? throw new NotFoundException("TravelPayment", id);
}

/// <summary>Turns requested lines into validated writes: source lines must be billable for the customer in the invoice currency.</summary>
public static class TravelInvoicePlanner
{
    public sealed record Plan(IReadOnlyList<TravelInvoiceLineWrite> Lines, decimal Subtotal, decimal Total, int? BookingId, int? VisaCaseId, int? GroupId);

    public static (Plan? Plan, string? Error) Build(IReadOnlyList<SaveTravelInvoiceLineDto> lines, decimal discount,
        IReadOnlyList<TravelBillableItemDto> billable, string currencyCode)
    {
        if (lines.Count == 0) return (null, "Add at least one line.");
        var sources = billable.ToDictionary(b => (b.SourceType, b.SourceId));
        var seen = new HashSet<(string, int)>();
        var writes = new List<TravelInvoiceLineWrite>();
        var contexts = new List<(string Type, int Id)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var description = l.Description?.Trim() ?? string.Empty;
            if (l.SourceType is not null || l.SourceId is not null)
            {
                if (l.SourceType is null || l.SourceId is not int sourceId || !TravelValues.IsOneOf(l.SourceType, TravelValues.InvoiceSourceTypes))
                    return (null, $"Line {i + 1}: unknown source.");
                if (!seen.Add((l.SourceType, sourceId))) return (null, $"Line {i + 1}: the same item is listed twice.");
                if (!sources.TryGetValue((l.SourceType, sourceId), out var item))
                    return (null, $"Line {i + 1}: the item is already invoiced, cancelled, has no price, or belongs to another customer.");
                if (item.CurrencyCode != currencyCode)
                    return (null, $"Line {i + 1}: the item is priced in {item.CurrencyCode}; this invoice is in {currencyCode}.");
                if (l.Quantity != 1) return (null, $"Line {i + 1}: a sold item is invoiced once (quantity 1).");
                contexts.Add((item.ContextType, item.ContextId));
                if (description.Length == 0) description = item.Description;
            }
            if (description.Length == 0) return (null, $"Line {i + 1}: a description is required.");
            if (l.Quantity <= 0 || l.UnitPrice < 0) return (null, $"Line {i + 1}: quantity must be positive and price not negative.");
            writes.Add(new TravelInvoiceLineWrite(i + 1, description.Length > 300 ? description[..300] : description,
                l.Quantity, TravelFinanceRules.Round(l.UnitPrice), TravelFinanceRules.LineTotal(l.Quantity, l.UnitPrice), l.SourceType, l.SourceId));
        }
        var subtotal = writes.Sum(w => w.LineTotal);
        var discountRounded = TravelFinanceRules.Round(discount);
        if (discountRounded < 0 || discountRounded > subtotal) return (null, "The discount must be between zero and the subtotal.");

        // The invoice carries a context (booking / visa case / group) when every sourced line comes from the same one.
        var distinct = contexts.Distinct().ToList();
        var single = distinct.Count == 1 ? distinct[0] : default;
        return (new Plan(writes, subtotal, subtotal - discountRounded,
            single.Type == "Booking" ? single.Id : null, single.Type == "VisaCase" ? single.Id : null, single.Type == "Group" ? single.Id : null), null);
    }
}

public class SaveTravelInvoiceLineValidator : AbstractValidator<SaveTravelInvoiceLineDto>
{
    public SaveTravelInvoiceLineValidator()
    {
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(10_000);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).LessThan(1_000_000_000_000m);
    }
}

// ── Create ──────────────────────────────────────────────────────────────

public record CreateTravelInvoiceCommand(CreateTravelInvoiceDto Invoice) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CreateInvoice";
    public string AuditEntityName => "TravelInvoice";
    public int? AuditEntityId => null;
}

public class CreateTravelInvoiceCommandValidator : AbstractValidator<CreateTravelInvoiceCommand>
{
    public CreateTravelInvoiceCommandValidator()
    {
        RuleFor(x => x.Invoice).NotNull();
        RuleFor(x => x.Invoice.CustomerId).GreaterThan(0);
        RuleFor(x => x.Invoice.CurrencyCode).Must(TravelValues.IsCurrencyCode).WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.Invoice.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Invoice.Notes).MaximumLength(2000);
        RuleFor(x => x.Invoice.Lines).NotEmpty().Must(l => l.Count <= 200);
        RuleForEach(x => x.Invoice.Lines).SetValidator(new SaveTravelInvoiceLineValidator());
        RuleFor(x => x.Invoice.Installments).NotNull().Must(i => i.Count <= 24).WithMessage("At most 24 installments.");
        RuleFor(x => x.Invoice.DueDate).GreaterThanOrEqualTo(x => x.Invoice.IssueDate)
            .When(x => x.Invoice.IssueDate.HasValue && x.Invoice.DueDate.HasValue).WithMessage("Due date cannot be before the issue date.");
    }
}

public class CreateTravelInvoiceCommandHandler(ITravelInvoiceRepository repository, ITravelCustomerRepository customers,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<CreateTravelInvoiceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Invoice;
        var customer = await customers.GetByIdAsync(dto.CustomerId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
                       ?? throw new NotFoundException("TravelCustomer", dto.CustomerId);
        var currency = dto.CurrencyCode.ToUpperInvariant();

        var billable = await repository.GetBillableAsync(tenantId, TravelBranchScope.CompanyWide, customer.CustomerId, currency, null, cancellationToken);
        var (plan, error) = TravelInvoicePlanner.Build(dto.Lines, dto.DiscountAmount, billable, currency);
        if (error is not null) return ApiResponse<int>.FailResponse(error);
        var installmentError = TravelFinanceRules.ValidateInstallments(dto.Installments, plan!.Total, dto.IssueDate);
        if (installmentError is not null) return ApiResponse<int>.FailResponse(installmentError);

        var header = new TravelInvoiceHeaderWrite(customer.CustomerId, currency, dto.IssueDate?.Date, dto.DueDate?.Date, plan.Subtotal,
            TravelFinanceRules.Round(dto.DiscountAmount), plan.Total, string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            plan.BookingId, plan.VisaCaseId, plan.GroupId, customer.BranchId);
        var id = await repository.CreateAsync(tenantId, header, plan.Lines, dto.Installments, currentUser.UserId, cancellationToken);
        if (id == 0) return ApiResponse<int>.FailResponse("One of the items was invoiced by someone else meanwhile. Reload the billable items.");
        return ApiResponse<int>.SuccessResponse(id, "Draft invoice saved.");
    }
}

// ── Update draft ────────────────────────────────────────────────────────

public record UpdateTravelInvoiceCommand(int Id, UpdateTravelInvoiceDto Invoice) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateInvoice";
    public string AuditEntityName => "TravelInvoice";
    public int? AuditEntityId => Id;
}

public class UpdateTravelInvoiceCommandValidator : AbstractValidator<UpdateTravelInvoiceCommand>
{
    public UpdateTravelInvoiceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Invoice).NotNull();
        RuleFor(x => x.Invoice.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Invoice.Notes).MaximumLength(2000);
        RuleFor(x => x.Invoice.Lines).NotEmpty().Must(l => l.Count <= 200);
        RuleForEach(x => x.Invoice.Lines).SetValidator(new SaveTravelInvoiceLineValidator());
        RuleFor(x => x.Invoice.Installments).NotNull().Must(i => i.Count <= 24).WithMessage("At most 24 installments.");
        RuleFor(x => x.Invoice.DueDate).GreaterThanOrEqualTo(x => x.Invoice.IssueDate)
            .When(x => x.Invoice.IssueDate.HasValue && x.Invoice.DueDate.HasValue).WithMessage("Due date cannot be before the issue date.");
    }
}

public class UpdateTravelInvoiceCommandHandler(ITravelInvoiceRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateTravelInvoiceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetInvoiceAsync(request.Id, tenantId, cancellationToken);
        if (state.Status != "Draft") return ApiResponse<bool>.FailResponse("Issued invoices cannot be edited — issue a credit note instead.");
        var dto = request.Invoice;

        var billable = await repository.GetBillableAsync(tenantId, TravelBranchScope.CompanyWide, state.CustomerId, state.CurrencyCode, state.Id, cancellationToken);
        var (plan, error) = TravelInvoicePlanner.Build(dto.Lines, dto.DiscountAmount, billable, state.CurrencyCode);
        if (error is not null) return ApiResponse<bool>.FailResponse(error);
        var installmentError = TravelFinanceRules.ValidateInstallments(dto.Installments, plan!.Total, dto.IssueDate);
        if (installmentError is not null) return ApiResponse<bool>.FailResponse(installmentError);

        var header = new TravelInvoiceHeaderWrite(state.CustomerId, state.CurrencyCode, dto.IssueDate?.Date, dto.DueDate?.Date, plan.Subtotal,
            TravelFinanceRules.Round(dto.DiscountAmount), plan.Total, string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            plan.BookingId, plan.VisaCaseId, plan.GroupId, state.BranchId);
        if (!await repository.UpdateDraftAsync(tenantId, state.Id, header, plan.Lines, dto.Installments, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Draft invoice saved.");
    }
}

// ── Issue ───────────────────────────────────────────────────────────────

public record IssueTravelInvoiceCommand(int Id, IssueTravelInvoiceDto Issue) : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => "IssueInvoice";
    public string AuditEntityName => "TravelInvoice";
    public int? AuditEntityId => Id;
}

public class IssueTravelInvoiceCommandHandler(ITravelInvoiceRepository repository, ITravelConfigRepository config, TravelFinanceGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<IssueTravelInvoiceCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(IssueTravelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetInvoiceAsync(request.Id, tenantId, cancellationToken);
        if (state.Status != "Draft") return ApiResponse<string>.FailResponse("Only a draft can be issued.");
        if (state.Total <= 0) return ApiResponse<string>.FailResponse("An invoice needs a total above zero.");

        var now = DateTime.UtcNow;
        var issueDate = (request.Issue?.IssueDate ?? state.IssueDate ?? now).Date;
        DateTime dueDate;
        if (state.InstallmentCount > 0)
        {
            if (state.InstallmentTotal != state.Total) return ApiResponse<string>.FailResponse("Installments no longer add up to the total — edit the draft.");
            if (state.FirstInstallmentDate < issueDate) return ApiResponse<string>.FailResponse("An installment falls before the issue date.");
            dueDate = state.LastInstallmentDate!.Value.Date;
        }
        else
        {
            var terms = (await config.GetSettingsAsync(tenantId, cancellationToken)).DefaultPaymentTermsDays;
            dueDate = (request.Issue?.DueDate ?? state.DueDate ?? issueDate.AddDays(terms)).Date;
        }
        if (dueDate < issueDate) return ApiResponse<string>.FailResponse("Due date cannot be before the issue date.");

        var number = await repository.IssueAsync(tenantId, state.Id, issueDate, dueDate, currentUser.UserId, now, cancellationToken)
                     ?? throw new ConflictException(TravelFinanceGuard.ConflictMessage);
        return ApiResponse<string>.SuccessResponse(number, $"Invoice {number} issued.");
    }
}

// ── Void / delete ───────────────────────────────────────────────────────

public record VoidTravelInvoiceCommand(int Id, VoidTravelInvoiceDto Void) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "VoidInvoice";
    public string AuditEntityName => "TravelInvoice";
    public int? AuditEntityId => Id;
}

public class VoidTravelInvoiceCommandValidator : AbstractValidator<VoidTravelInvoiceCommand>
{
    public VoidTravelInvoiceCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Void.Reason).NotEmpty().MaximumLength(500);
    }
}

public class VoidTravelInvoiceCommandHandler(ITravelInvoiceRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<VoidTravelInvoiceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(VoidTravelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetInvoiceAsync(request.Id, tenantId, cancellationToken);
        if (state.Status != "Issued") return ApiResponse<bool>.FailResponse("Only an issued invoice can be voided (delete a draft instead).");
        if (!TravelInvoiceStateMachine.CanVoid(state))
            return ApiResponse<bool>.FailResponse("The invoice has payments, credit notes or refunds — issue a credit note instead of voiding.");
        if (!await repository.VoidAsync(tenantId, state.Id, request.Void.Reason.Trim(), currentUser.UserId, DateTime.UtcNow, cancellationToken))
            return ApiResponse<bool>.FailResponse("The invoice received a payment or credit meanwhile and cannot be voided.");
        return ApiResponse<bool>.SuccessResponse(true, "Invoice voided; its items can be invoiced again.");
    }
}

public record DeleteTravelInvoiceCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteDraftInvoice";
    public string AuditEntityName => "TravelInvoice";
    public int? AuditEntityId => Id;
}

public class DeleteTravelInvoiceCommandHandler(ITravelInvoiceRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelInvoiceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetInvoiceAsync(request.Id, tenantId, cancellationToken);
        if (state.Status != "Draft") return ApiResponse<bool>.FailResponse("Only drafts can be deleted — void an issued invoice.");
        if (!await repository.DeleteDraftAsync(tenantId, state.Id, cancellationToken)) throw new ConflictException(TravelFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Draft deleted.");
    }
}
