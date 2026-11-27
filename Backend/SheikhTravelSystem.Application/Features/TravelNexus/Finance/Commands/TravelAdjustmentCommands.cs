using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;

// ── Credit notes ────────────────────────────────────────────────────────

/// <summary>Reduces an issued invoice (up to its uncredited total). Optionally records which Phase 5 cancellation it settles.</summary>
public record IssueTravelCreditNoteCommand(IssueTravelCreditNoteDto CreditNote) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "IssueCreditNote";
    public string AuditEntityName => "TravelInvoice";
    public int? AuditEntityId => CreditNote.InvoiceId;
}

public class IssueTravelCreditNoteCommandValidator : AbstractValidator<IssueTravelCreditNoteCommand>
{
    public IssueTravelCreditNoteCommandValidator()
    {
        RuleFor(x => x.CreditNote).NotNull();
        RuleFor(x => x.CreditNote.IdempotencyKey).NotEqual(Guid.Empty).WithMessage("A client request key is required.");
        RuleFor(x => x.CreditNote.InvoiceId).GreaterThan(0);
        RuleFor(x => x.CreditNote.Amount).GreaterThan(0);
        RuleFor(x => x.CreditNote.Reason).NotEmpty().MaximumLength(500);
    }
}

public class IssueTravelCreditNoteCommandHandler(ITravelPaymentRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<IssueTravelCreditNoteCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(IssueTravelCreditNoteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.CreditNote;
        var invoice = await guard.GetInvoiceAsync(dto.InvoiceId, tenantId, cancellationToken);
        if (await repository.FindByIdempotencyKeyAsync(tenantId, TravelIdempotentRecord.CreditNote, dto.IdempotencyKey, cancellationToken) is int replay)
            return ApiResponse<int>.SuccessResponse(replay, "This credit note was already issued.");
        if (invoice.Status != "Issued") return ApiResponse<int>.FailResponse("Credit notes are issued against issued invoices.");
        var amount = TravelFinanceRules.Round(dto.Amount);
        var creditable = invoice.Total - invoice.Credited;
        if (amount > creditable) return ApiResponse<int>.FailResponse($"At most {creditable:0.##} {invoice.CurrencyCode} can still be credited on this invoice.");

        var cancellationError = await TravelCancellationLink.CheckAsync(repository, tenantId, dto.CancellationId, invoice.CustomerId, invoice.CurrencyCode, cancellationToken);
        if (cancellationError is not null) return ApiResponse<int>.FailResponse(cancellationError);

        var result = await repository.IssueCreditNoteAsync(tenantId, dto.IdempotencyKey, invoice.Id, amount, dto.Reason.Trim(), dto.CancellationId,
            (dto.IssuedOn ?? DateTime.UtcNow).Date, currentUser.UserId, DateTime.UtcNow, cancellationToken);
        if (result.Error is not null) return ApiResponse<int>.FailResponse(result.Error);
        return ApiResponse<int>.SuccessResponse(result.Id, result.Replayed ? "This credit note was already issued." : "Credit note issued.");
    }
}

public record VoidTravelCreditNoteCommand(int CreditNoteId, VoidTravelCreditNoteDto Void) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "VoidCreditNote";
    public string AuditEntityName => "TravelCreditNote";
    public int? AuditEntityId => CreditNoteId;
}

public class VoidTravelCreditNoteCommandValidator : AbstractValidator<VoidTravelCreditNoteCommand>
{
    public VoidTravelCreditNoteCommandValidator()
    {
        RuleFor(x => x.CreditNoteId).GreaterThan(0);
        RuleFor(x => x.Void.Reason).NotEmpty().MaximumLength(500);
    }
}

public class VoidTravelCreditNoteCommandHandler(ITravelPaymentRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<VoidTravelCreditNoteCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(VoidTravelCreditNoteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var note = await repository.GetCreditNoteAsync(request.CreditNoteId, tenantId, cancellationToken)
                   ?? throw new NotFoundException("TravelCreditNote", request.CreditNoteId);
        var invoice = await guard.GetInvoiceAsync(note.InvoiceId, tenantId, cancellationToken);
        if (note.Status != "Issued") return ApiResponse<bool>.FailResponse("The credit note is already void.");
        if (invoice.RefundCount > 0) return ApiResponse<bool>.FailResponse("Money was refunded on this invoice; a credit note behind a refund cannot be voided.");
        if (!await repository.VoidCreditNoteAsync(tenantId, note.Id, request.Void.Reason.Trim(), currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Credit note voided.");
    }
}

// ── Refunds ─────────────────────────────────────────────────────────────

public record RecordTravelRefundCommand(RecordTravelRefundDto Refund) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "RecordRefund";
    public string AuditEntityName => "TravelRefund";
    public int? AuditEntityId => null;
}

public class RecordTravelRefundCommandValidator : AbstractValidator<RecordTravelRefundCommand>
{
    public RecordTravelRefundCommandValidator()
    {
        RuleFor(x => x.Refund).NotNull();
        RuleFor(x => x.Refund.IdempotencyKey).NotEqual(Guid.Empty).WithMessage("A client request key is required.");
        RuleFor(x => x.Refund).Must(r => r.InvoiceId.HasValue ^ r.PaymentId.HasValue).WithMessage("Refund either an invoice's credit or a payment's advance.");
        RuleFor(x => x.Refund.Amount).GreaterThan(0);
        RuleFor(x => x.Refund.Method).Must(m => TravelValues.IsOneOf(m, TravelValues.PaymentMethods)).WithMessage("Unknown payment method.");
        RuleFor(x => x.Refund.PaidOn).Must(d => d.Date <= DateTime.UtcNow.Date.AddDays(1)).WithMessage("A refund cannot be paid in the future.");
        RuleFor(x => x.Refund.Reference).MaximumLength(100);
        RuleFor(x => x.Refund.Notes).MaximumLength(1000);
    }
}

public class RecordTravelRefundCommandHandler(ITravelPaymentRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<RecordTravelRefundCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RecordTravelRefundCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Refund with { Amount = TravelFinanceRules.Round(request.Refund.Amount) };
        // Checked after the invoice / payment lookup (scope), before any balance check the first write changed.
        async Task<ApiResponse<int>?> ReplayAsync()
            => await repository.FindByIdempotencyKeyAsync(tenantId, TravelIdempotentRecord.CustomerRefund, dto.IdempotencyKey, cancellationToken) is int id
                ? ApiResponse<int>.SuccessResponse(id, "This refund was already recorded.") : null;
        int customerId;
        string currency;
        if (dto.InvoiceId is int invoiceId)
        {
            var invoice = await guard.GetInvoiceAsync(invoiceId, tenantId, cancellationToken);
            if (await ReplayAsync() is { } replayed) return replayed;
            if (invoice.Status != "Issued") return ApiResponse<int>.FailResponse("Refunds are paid against issued invoices.");
            var creditDue = -invoice.Balance;
            if (dto.Amount > creditDue)
                return ApiResponse<int>.FailResponse(creditDue > 0
                    ? $"Only {creditDue:0.##} {invoice.CurrencyCode} is owed back on this invoice."
                    : "Nothing is owed back on this invoice — issue a credit note first.");
            (customerId, currency) = (invoice.CustomerId, invoice.CurrencyCode);
        }
        else
        {
            var payment = await guard.GetPaymentAsync(dto.PaymentId!.Value, tenantId, cancellationToken);
            if (await ReplayAsync() is { } replayed) return replayed;
            if (payment.Status != "Completed") return ApiResponse<int>.FailResponse("Only a cleared payment's advance can be refunded.");
            if (dto.Amount > payment.Unallocated)
                return ApiResponse<int>.FailResponse($"Only {payment.Unallocated:0.##} {payment.CurrencyCode} of this payment is unallocated.");
            (customerId, currency) = (payment.CustomerId, payment.CurrencyCode);
        }

        var cancellationError = await TravelCancellationLink.CheckAsync(repository, tenantId, dto.CancellationId, customerId, currency, cancellationToken);
        if (cancellationError is not null) return ApiResponse<int>.FailResponse(cancellationError);
        var advance = false;
        if (dto.CancellationId is int cancellationId)
        {
            var facts = await repository.GetCancellationFactsAsync(tenantId, cancellationId, cancellationToken);
            advance = facts is not null && TravelRefundStateMachine.Next(facts.RefundStatus).Contains("PaidToCustomer");
        }

        var result = await repository.RecordRefundAsync(tenantId, dto, customerId, currency, currentUser.UserId, DateTime.UtcNow, advance, cancellationToken);
        if (result.Error is not null) return ApiResponse<int>.FailResponse(result.Error);
        return ApiResponse<int>.SuccessResponse(result.Id, result.Replayed ? "This refund was already recorded."
            : advance ? "Refund recorded; the cancellation is marked paid to the customer." : "Refund recorded.");
    }
}

internal static class TravelCancellationLink
{
    /// <summary>A linked cancellation must be of this tenant, on a booking of the same customer, in the same currency.</summary>
    public static async Task<string?> CheckAsync(ITravelPaymentRepository repository, int tenantId, int? cancellationId, int customerId, string currency,
        CancellationToken ct)
    {
        if (cancellationId is not int id) return null;
        var facts = await repository.GetCancellationFactsAsync(tenantId, id, ct) ?? throw new NotFoundException("TravelBookingCancellation", id);
        if (facts.CustomerId != customerId) return "The cancellation belongs to another customer's booking.";
        if (facts.CurrencyCode is not null && facts.CurrencyCode != currency) return $"The cancellation is in {facts.CurrencyCode}, not {currency}.";
        return null;
    }
}
