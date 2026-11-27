using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;

public class TravelAllocationInputValidator : AbstractValidator<TravelAllocationInputDto>
{
    public TravelAllocationInputValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.AmountFromPayment).GreaterThan(0);
        RuleFor(x => x.ExchangeRate).GreaterThan(0).LessThan(1_000_000).When(x => x.ExchangeRate.HasValue);
    }
}

internal static class TravelAllocationLoader
{
    /// <summary>Open issued invoices of the customer keyed by id, with what may still be applied.</summary>
    public static async Task<IReadOnlyDictionary<int, (string Currency, decimal Available)>> OpenInvoicesAsync(
        ITravelInvoiceRepository invoices, int tenantId, int customerId, CancellationToken ct)
        => (await invoices.GetOpenInvoicesAsync(tenantId, customerId, ct))
            .ToDictionary(i => i.Id, i => (i.CurrencyCode, TravelFinanceRules.Available(i)));
}

// ── Record ──────────────────────────────────────────────────────────────

/// <summary>Records a receipt (optionally applied to invoices). Resubmitting the same idempotency key returns the original receipt.</summary>
public record RecordTravelPaymentCommand(RecordTravelPaymentDto Payment) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "RecordPayment";
    public string AuditEntityName => "TravelPayment";
    public int? AuditEntityId => null;
}

public class RecordTravelPaymentCommandValidator : AbstractValidator<RecordTravelPaymentCommand>
{
    public RecordTravelPaymentCommandValidator()
    {
        RuleFor(x => x.Payment).NotNull();
        RuleFor(x => x.Payment.IdempotencyKey).NotEqual(Guid.Empty).WithMessage("A client request key is required.");
        RuleFor(x => x.Payment.CustomerId).GreaterThan(0);
        RuleFor(x => x.Payment.Amount).GreaterThan(0).LessThan(1_000_000_000_000m);
        RuleFor(x => x.Payment.CurrencyCode).Must(TravelValues.IsCurrencyCode).WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.Payment.Method).Must(m => TravelValues.IsOneOf(m, TravelValues.PaymentMethods)).WithMessage("Unknown payment method.");
        RuleFor(x => x.Payment.ReceivedOn).Must(d => d.Date <= DateTime.UtcNow.Date.AddDays(1)).WithMessage("A payment cannot be received in the future.");
        RuleFor(x => x.Payment.Reference).MaximumLength(100);
        RuleFor(x => x.Payment.Notes).MaximumLength(1000);
        RuleFor(x => x.Payment.Allocations).NotNull().Must(a => a.Count <= 50);
        RuleForEach(x => x.Payment.Allocations).SetValidator(new TravelAllocationInputValidator());
    }
}

public class RecordTravelPaymentCommandHandler(ITravelPaymentRepository repository, ITravelInvoiceRepository invoices, ITravelCustomerRepository customers,
    ITravelBranchScopeResolver scopeResolver, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<RecordTravelPaymentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RecordTravelPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Payment;
        var customer = await customers.GetByIdAsync(dto.CustomerId, tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), cancellationToken)
                       ?? throw new NotFoundException("TravelCustomer", dto.CustomerId);
        // A retry of a recorded receipt returns it before re-validating against balances the first write changed.
        if (await repository.FindByIdempotencyKeyAsync(tenantId, TravelIdempotentRecord.Receipt, dto.IdempotencyKey, cancellationToken) is int replay)
            return ApiResponse<int>.SuccessResponse(replay, "This payment was already recorded.");
        var currency = dto.CurrencyCode.ToUpperInvariant();
        var amount = TravelFinanceRules.Round(dto.Amount);

        var open = await TravelAllocationLoader.OpenInvoicesAsync(invoices, tenantId, customer.CustomerId, cancellationToken);
        var (allocations, error) = TravelFinanceRules.PlanAllocations(dto.Allocations, currency, amount, open);
        if (error is not null) return ApiResponse<int>.FailResponse(error);

        var write = new TravelPaymentWrite(dto.IdempotencyKey, customer.CustomerId, dto.ReceivedOn.Date, dto.Method,
            string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim(), amount, currency, dto.Cleared ? "Completed" : "Pending",
            string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(), customer.BranchId);
        var result = await repository.RecordAsync(tenantId, write, allocations, currentUser.UserId, DateTime.UtcNow, cancellationToken);
        if (result.Error is not null) return ApiResponse<int>.FailResponse(result.Error);
        return ApiResponse<int>.SuccessResponse(result.Id, result.Replayed ? "This payment was already recorded." : "Payment recorded.");
    }
}

// ── Allocate / unallocate ───────────────────────────────────────────────

public record AllocateTravelPaymentCommand(int PaymentId, AllocateTravelPaymentDto Allocation) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AllocatePayment";
    public string AuditEntityName => "TravelPayment";
    public int? AuditEntityId => PaymentId;
}

public class AllocateTravelPaymentCommandValidator : AbstractValidator<AllocateTravelPaymentCommand>
{
    public AllocateTravelPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.Allocation.Allocations).NotEmpty().Must(a => a.Count <= 50);
        RuleForEach(x => x.Allocation.Allocations).SetValidator(new TravelAllocationInputValidator());
    }
}

public class AllocateTravelPaymentCommandHandler(ITravelPaymentRepository repository, ITravelInvoiceRepository invoices, TravelFinanceGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<AllocateTravelPaymentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AllocateTravelPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payment = await guard.GetPaymentAsync(request.PaymentId, tenantId, cancellationToken);
        if (payment.Status is not ("Pending" or "Completed")) return ApiResponse<bool>.FailResponse($"A {payment.Status.ToLowerInvariant()} payment cannot be allocated.");
        var open = await TravelAllocationLoader.OpenInvoicesAsync(invoices, tenantId, payment.CustomerId, cancellationToken);
        var (allocations, error) = TravelFinanceRules.PlanAllocations(request.Allocation.Allocations, payment.CurrencyCode, payment.Unallocated, open);
        if (error is not null) return ApiResponse<bool>.FailResponse(error);
        var result = await repository.AllocateAsync(tenantId, payment.Id, allocations, currentUser.UserId, cancellationToken);
        if (result.Error is not null) return ApiResponse<bool>.FailResponse(result.Error);
        return ApiResponse<bool>.SuccessResponse(true, "Payment applied.");
    }
}

public record UnallocateTravelPaymentCommand(int PaymentId, int InvoiceId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UnallocatePayment";
    public string AuditEntityName => "TravelPayment";
    public int? AuditEntityId => PaymentId;
}

public class UnallocateTravelPaymentCommandHandler(ITravelPaymentRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<UnallocateTravelPaymentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UnallocateTravelPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payment = await guard.GetPaymentAsync(request.PaymentId, tenantId, cancellationToken);
        if (payment.ReconciledAt is not null) return ApiResponse<bool>.FailResponse("A reconciled payment cannot be re-allocated.");
        if (!await repository.UnallocateAsync(tenantId, payment.Id, request.InvoiceId, currentUser.UserId, cancellationToken))
            throw new NotFoundException("TravelPaymentAllocation", request.InvoiceId);
        return ApiResponse<bool>.SuccessResponse(true, "Allocation removed.");
    }
}

// ── Clear / fail ────────────────────────────────────────────────────────

public record ChangeTravelPaymentStatusCommand(int PaymentId, ChangeTravelPaymentStatusDto Change) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => $"Payment{Change.Status}";
    public string AuditEntityName => "TravelPayment";
    public int? AuditEntityId => PaymentId;
}

public class ChangeTravelPaymentStatusCommandValidator : AbstractValidator<ChangeTravelPaymentStatusCommand>
{
    public ChangeTravelPaymentStatusCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.Change.Status).Must(s => s is "Completed" or "Failed").WithMessage("A payment is cleared (Completed) or failed.");
        RuleFor(x => x.Change.Note).NotEmpty().When(x => x.Change.Status == "Failed").WithMessage("Say why the payment failed.");
        RuleFor(x => x.Change.Note).MaximumLength(500);
    }
}

/// <summary>Pending → Completed (cleared) or Failed (bounced); Completed → Failed (reversed) while not reconciled or refunded.</summary>
public class ChangeTravelPaymentStatusCommandHandler(ITravelPaymentRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<ChangeTravelPaymentStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangeTravelPaymentStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payment = await guard.GetPaymentAsync(request.PaymentId, tenantId, cancellationToken);
        var next = request.Change.Status;
        var allowed = (payment.Status, next) switch
        {
            ("Pending", "Completed") or ("Pending", "Failed") => true,
            ("Completed", "Failed") => payment.ReconciledAt is null && payment.Refunded == 0,
            _ => false
        };
        if (!allowed) return ApiResponse<bool>.FailResponse(payment.Status == "Completed" && next == "Failed"
            ? "A reconciled or refunded payment cannot be marked failed."
            : $"A {payment.Status.ToLowerInvariant()} payment cannot become {next.ToLowerInvariant()}.");
        if (!await repository.ChangeStatusAsync(tenantId, payment.Id, payment.Status, next, request.Change.Note?.Trim(), currentUser.UserId,
                DateTime.UtcNow, cancellationToken))
            throw new ConflictException(TravelFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, next == "Completed" ? "Payment cleared." : "Payment marked failed; its allocations no longer count.");
    }
}

// ── Reconcile ───────────────────────────────────────────────────────────

public record ReconcileTravelPaymentCommand(int PaymentId, ReconcileTravelPaymentDto Reconcile) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "ReconcilePayment";
    public string AuditEntityName => "TravelPayment";
    public int? AuditEntityId => PaymentId;
}

public class ReconcileTravelPaymentCommandValidator : AbstractValidator<ReconcileTravelPaymentCommand>
{
    public ReconcileTravelPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.Reconcile.BankReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Reconcile.Note).MaximumLength(500);
    }
}

public class ReconcileTravelPaymentCommandHandler(ITravelPaymentRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<ReconcileTravelPaymentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ReconcileTravelPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payment = await guard.GetPaymentAsync(request.PaymentId, tenantId, cancellationToken);
        if (payment.Status is not ("Completed" or "Refunded")) return ApiResponse<bool>.FailResponse("Only cleared payments are reconciled.");
        if (payment.ReconciledAt is not null) return ApiResponse<bool>.FailResponse("The payment is already reconciled.");
        if (!await repository.ReconcileAsync(tenantId, payment.Id, request.Reconcile.BankReference.Trim(), request.Reconcile.Note?.Trim(),
                currentUser.UserId, DateTime.UtcNow, cancellationToken))
            throw new ConflictException(TravelFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Payment reconciled.");
    }
}
