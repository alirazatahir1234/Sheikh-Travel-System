using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Commands;

internal static class SupplierAllocations
{
    public static async Task<IReadOnlyDictionary<int, (string Currency, decimal Available)>> OpenBillsAsync(ITravelSupplierBillRepository bills, int tenantId,
        int supplierId, CancellationToken ct)
        => (await bills.GetOpenBillsAsync(tenantId, supplierId, ct)).ToDictionary(b => b.Id, b => (b.CurrencyCode, TravelFinanceRules.Available(b)));

    /// <summary>A linked cancellation must be on a service of this supplier, in the same currency.</summary>
    public static async Task<(string? Error, bool Advance)> CheckCancellationAsync(ITravelSupplierPaymentRepository payments, int tenantId, int? cancellationId,
        int supplierId, string currency, CancellationToken ct)
    {
        if (cancellationId is not int id) return (null, false);
        var facts = await payments.GetCancellationSupplierAsync(tenantId, id, ct) ?? throw new NotFoundException("TravelBookingCancellation", id);
        if (facts.SupplierId != supplierId) return ("The cancellation is on another supplier's service.", false);
        if (facts.CurrencyCode is not null && facts.CurrencyCode != currency) return ($"The cancellation is in {facts.CurrencyCode}, not {currency}.", false);
        return (null, TravelRefundStateMachine.Next(facts.RefundStatus).Contains("ReceivedFromSupplier"));
    }
}

// ── Record / allocate ───────────────────────────────────────────────────

public record RecordTravelSupplierPaymentCommand(RecordTravelSupplierPaymentDto Payment) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "RecordSupplierPayment";
    public string AuditEntityName => "TravelSupplierPayment";
    public int? AuditEntityId => null;
}

public class RecordTravelSupplierPaymentCommandValidator : AbstractValidator<RecordTravelSupplierPaymentCommand>
{
    public RecordTravelSupplierPaymentCommandValidator()
    {
        RuleFor(x => x.Payment.IdempotencyKey).NotEqual(Guid.Empty).WithMessage("A client request key is required.");
        RuleFor(x => x.Payment.SupplierId).GreaterThan(0);
        RuleFor(x => x.Payment.Amount).GreaterThan(0).LessThan(1_000_000_000_000m);
        RuleFor(x => x.Payment.CurrencyCode).Must(c => TravelValues.IsCurrencyCode(c?.ToUpperInvariant())).WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.Payment.Method).Must(m => TravelValues.IsOneOf(m, TravelValues.PaymentMethods)).WithMessage("Unknown payment method.");
        RuleFor(x => x.Payment.PaidOn).Must(d => d.Date <= DateTime.UtcNow.Date.AddDays(1)).WithMessage("A payment cannot be made in the future.");
        RuleFor(x => x.Payment.Reference).MaximumLength(100);
        RuleFor(x => x.Payment.Notes).MaximumLength(1000);
        RuleFor(x => x.Payment.Allocations).NotNull().Must(a => a.Count <= 50);
        RuleForEach(x => x.Payment.Allocations).SetValidator(new TravelAllocationInputValidator());
    }
}

public class RecordTravelSupplierPaymentCommandHandler(ITravelSupplierPaymentRepository repository, ITravelSupplierBillRepository bills,
    ITravelSupplierRepository suppliers, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<RecordTravelSupplierPaymentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RecordTravelSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Payment;
        _ = await suppliers.GetByIdAsync(dto.SupplierId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplier", dto.SupplierId);
        // A retry of a recorded payment returns it before re-validating against bills the first write settled.
        if (await repository.FindByIdempotencyKeyAsync(tenantId, TravelIdempotentRecord.SupplierPayment, dto.IdempotencyKey, cancellationToken) is int replay)
            return ApiResponse<int>.SuccessResponse(replay, "This payment was already recorded.");
        var currency = dto.CurrencyCode.ToUpperInvariant();
        var amount = TravelFinanceRules.Round(dto.Amount);
        var open = await SupplierAllocations.OpenBillsAsync(bills, tenantId, dto.SupplierId, cancellationToken);
        var (allocations, error) = TravelFinanceRules.PlanAllocations(dto.Allocations, currency, amount, open);
        if (error is not null) return ApiResponse<int>.FailResponse(error.Replace("invoice", "bill"));
        var write = new TravelPaymentWrite(dto.IdempotencyKey, dto.SupplierId, dto.PaidOn.Date, dto.Method,
            string.IsNullOrWhiteSpace(dto.Reference) ? null : dto.Reference.Trim(), amount, currency, dto.Cleared ? "Completed" : "Pending",
            string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(), null);
        var result = await repository.RecordAsync(tenantId, write, allocations, currentUser.UserId, DateTime.UtcNow, cancellationToken);
        if (result.Error is not null) return ApiResponse<int>.FailResponse(result.Error);
        return ApiResponse<int>.SuccessResponse(result.Id, result.Replayed ? "This payment was already recorded." : "Supplier payment recorded.");
    }
}

public record AllocateTravelSupplierPaymentCommand(int PaymentId, AllocateTravelPaymentDto Allocation) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AllocateSupplierPayment";
    public string AuditEntityName => "TravelSupplierPayment";
    public int? AuditEntityId => PaymentId;
}

public class AllocateTravelSupplierPaymentCommandValidator : AbstractValidator<AllocateTravelSupplierPaymentCommand>
{
    public AllocateTravelSupplierPaymentCommandValidator()
    {
        RuleFor(x => x.Allocation.Allocations).NotEmpty().Must(a => a.Count <= 50);
        RuleForEach(x => x.Allocation.Allocations).SetValidator(new TravelAllocationInputValidator());
    }
}

public class AllocateTravelSupplierPaymentCommandHandler(ITravelSupplierPaymentRepository repository, ITravelSupplierBillRepository bills,
    TravelSupplierFinanceGuard guard, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<AllocateTravelSupplierPaymentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AllocateTravelSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payment = await guard.GetPaymentAsync(request.PaymentId, tenantId, cancellationToken);
        if (payment.Status is not ("Pending" or "Completed")) return ApiResponse<bool>.FailResponse($"A {payment.Status.ToLowerInvariant()} payment cannot be allocated.");
        var open = await SupplierAllocations.OpenBillsAsync(bills, tenantId, payment.SupplierId, cancellationToken);
        var (allocations, error) = TravelFinanceRules.PlanAllocations(request.Allocation.Allocations, payment.CurrencyCode, payment.Unallocated, open);
        if (error is not null) return ApiResponse<bool>.FailResponse(error.Replace("invoice", "bill"));
        var result = await repository.AllocateAsync(tenantId, payment.Id, allocations, currentUser.UserId, cancellationToken);
        if (result.Error is not null) return ApiResponse<bool>.FailResponse(result.Error);
        return ApiResponse<bool>.SuccessResponse(true, "Payment applied.");
    }
}

public record UnallocateTravelSupplierPaymentCommand(int PaymentId, int BillId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UnallocateSupplierPayment";
    public string AuditEntityName => "TravelSupplierPayment";
    public int? AuditEntityId => PaymentId;
}

public class UnallocateTravelSupplierPaymentCommandHandler(ITravelSupplierPaymentRepository repository, TravelSupplierFinanceGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<UnallocateTravelSupplierPaymentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UnallocateTravelSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payment = await guard.GetPaymentAsync(request.PaymentId, tenantId, cancellationToken);
        if (payment.ReconciledAt is not null) return ApiResponse<bool>.FailResponse("A reconciled payment cannot be re-allocated.");
        if (!await repository.UnallocateAsync(tenantId, payment.Id, request.BillId, currentUser.UserId, cancellationToken))
            throw new NotFoundException("TravelSupplierPaymentAllocation", request.BillId);
        return ApiResponse<bool>.SuccessResponse(true, "Allocation removed.");
    }
}

public record ChangeTravelSupplierPaymentStatusCommand(int PaymentId, ChangeTravelPaymentStatusDto Change) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => $"SupplierPayment{Change.Status}";
    public string AuditEntityName => "TravelSupplierPayment";
    public int? AuditEntityId => PaymentId;
}

public class ChangeTravelSupplierPaymentStatusCommandValidator : AbstractValidator<ChangeTravelSupplierPaymentStatusCommand>
{
    public ChangeTravelSupplierPaymentStatusCommandValidator()
    {
        RuleFor(x => x.Change.Status).Must(s => s is "Completed" or "Failed").WithMessage("A payment is cleared (Completed) or failed.");
        RuleFor(x => x.Change.Note).NotEmpty().When(x => x.Change.Status == "Failed").WithMessage("Say why the payment failed.");
    }
}

public class ChangeTravelSupplierPaymentStatusCommandHandler(ITravelSupplierPaymentRepository repository, TravelSupplierFinanceGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<ChangeTravelSupplierPaymentStatusCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ChangeTravelSupplierPaymentStatusCommand request, CancellationToken cancellationToken)
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
        if (!allowed) return ApiResponse<bool>.FailResponse($"A {payment.Status.ToLowerInvariant()} payment cannot become {next.ToLowerInvariant()}.");
        if (!await repository.ChangeStatusAsync(tenantId, payment.Id, payment.Status, next, request.Change.Note?.Trim(), currentUser.UserId, DateTime.UtcNow, cancellationToken))
            throw new ConflictException(TravelSupplierFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, next == "Completed" ? "Payment cleared." : "Payment marked failed.");
    }
}

public record ReconcileTravelSupplierPaymentCommand(int PaymentId, ReconcileTravelPaymentDto Reconcile) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "ReconcileSupplierPayment";
    public string AuditEntityName => "TravelSupplierPayment";
    public int? AuditEntityId => PaymentId;
}

public class ReconcileTravelSupplierPaymentCommandValidator : AbstractValidator<ReconcileTravelSupplierPaymentCommand>
{
    public ReconcileTravelSupplierPaymentCommandValidator() => RuleFor(x => x.Reconcile.BankReference).NotEmpty().MaximumLength(100);
}

public class ReconcileTravelSupplierPaymentCommandHandler(ITravelSupplierPaymentRepository repository, TravelSupplierFinanceGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<ReconcileTravelSupplierPaymentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ReconcileTravelSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var payment = await guard.GetPaymentAsync(request.PaymentId, tenantId, cancellationToken);
        if (payment.Status is not ("Completed" or "Refunded") || payment.ReconciledAt is not null)
            return ApiResponse<bool>.FailResponse("Only a cleared, unreconciled payment can be reconciled.");
        if (!await repository.ReconcileAsync(tenantId, payment.Id, request.Reconcile.BankReference.Trim(), request.Reconcile.Note?.Trim(), currentUser.UserId,
                DateTime.UtcNow, cancellationToken))
            throw new ConflictException(TravelSupplierFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Payment reconciled.");
    }
}

// ── Supplier credits and refunds received ───────────────────────────────

public record IssueTravelSupplierCreditCommand(IssueTravelSupplierCreditDto Credit) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "IssueSupplierCredit";
    public string AuditEntityName => "TravelSupplierBill";
    public int? AuditEntityId => Credit.BillId;
}

public class IssueTravelSupplierCreditCommandValidator : AbstractValidator<IssueTravelSupplierCreditCommand>
{
    public IssueTravelSupplierCreditCommandValidator()
    {
        RuleFor(x => x.Credit.IdempotencyKey).NotEqual(Guid.Empty).WithMessage("A client request key is required.");
        RuleFor(x => x.Credit.Amount).GreaterThan(0);
        RuleFor(x => x.Credit.Reason).NotEmpty().MaximumLength(500);
    }
}

public class IssueTravelSupplierCreditCommandHandler(ITravelSupplierPaymentRepository repository, TravelSupplierFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<IssueTravelSupplierCreditCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(IssueTravelSupplierCreditCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Credit with { Amount = TravelFinanceRules.Round(request.Credit.Amount), Reason = request.Credit.Reason.Trim() };
        var bill = await guard.GetBillAsync(dto.BillId, tenantId, cancellationToken);
        if (await repository.FindByIdempotencyKeyAsync(tenantId, TravelIdempotentRecord.SupplierCredit, dto.IdempotencyKey, cancellationToken) is int replay)
            return ApiResponse<int>.SuccessResponse(replay, "This credit was already recorded.");
        if (bill.Status != "Posted") return ApiResponse<int>.FailResponse("Supplier credits are recorded against posted bills.");
        if (dto.Amount > bill.Total - bill.Credited) return ApiResponse<int>.FailResponse($"At most {bill.Total - bill.Credited:0.##} {bill.CurrencyCode} can still be credited.");
        var (error, advance) = await SupplierAllocations.CheckCancellationAsync(repository, tenantId, dto.CancellationId, bill.SupplierId, bill.CurrencyCode, cancellationToken);
        if (error is not null) return ApiResponse<int>.FailResponse(error);
        var result = await repository.IssueCreditAsync(tenantId, dto, (dto.IssuedOn ?? DateTime.UtcNow).Date, currentUser.UserId, DateTime.UtcNow, advance, cancellationToken);
        if (result.Error is not null) return ApiResponse<int>.FailResponse(result.Error);
        return ApiResponse<int>.SuccessResponse(result.Id, result.Replayed ? "This credit was already recorded." : "Supplier credit recorded.");
    }
}

public record VoidTravelSupplierCreditCommand(int CreditId, string Reason) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "VoidSupplierCredit";
    public string AuditEntityName => "TravelSupplierCredit";
    public int? AuditEntityId => CreditId;
}

public class VoidTravelSupplierCreditCommandValidator : AbstractValidator<VoidTravelSupplierCreditCommand>
{
    public VoidTravelSupplierCreditCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class VoidTravelSupplierCreditCommandHandler(ITravelSupplierPaymentRepository repository, TravelSupplierFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<VoidTravelSupplierCreditCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(VoidTravelSupplierCreditCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var credit = await repository.GetCreditAsync(request.CreditId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplierCredit", request.CreditId);
        var bill = await guard.GetBillAsync(credit.InvoiceId, tenantId, cancellationToken);
        if (credit.Status != "Issued") return ApiResponse<bool>.FailResponse("The credit is already void.");
        if (bill.RefundCount > 0) return ApiResponse<bool>.FailResponse("Money was received back on this bill; a credit behind a refund cannot be voided.");
        if (!await repository.VoidCreditAsync(tenantId, credit.Id, request.Reason.Trim(), currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelSupplierFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Supplier credit voided.");
    }
}

public record RecordTravelSupplierRefundCommand(RecordTravelSupplierRefundDto Refund) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "RecordSupplierRefund";
    public string AuditEntityName => "TravelSupplierRefund";
    public int? AuditEntityId => null;
}

public class RecordTravelSupplierRefundCommandValidator : AbstractValidator<RecordTravelSupplierRefundCommand>
{
    public RecordTravelSupplierRefundCommandValidator()
    {
        RuleFor(x => x.Refund.IdempotencyKey).NotEqual(Guid.Empty).WithMessage("A client request key is required.");
        RuleFor(x => x.Refund).Must(r => r.BillId.HasValue ^ r.PaymentId.HasValue).WithMessage("A refund is received against a bill's credit or a payment's advance.");
        RuleFor(x => x.Refund.Amount).GreaterThan(0);
        RuleFor(x => x.Refund.Method).Must(m => TravelValues.IsOneOf(m, TravelValues.PaymentMethods)).WithMessage("Unknown payment method.");
        RuleFor(x => x.Refund.ReceivedOn).Must(d => d.Date <= DateTime.UtcNow.Date.AddDays(1)).WithMessage("A refund cannot be received in the future.");
    }
}

public class RecordTravelSupplierRefundCommandHandler(ITravelSupplierPaymentRepository repository, TravelSupplierFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<RecordTravelSupplierRefundCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RecordTravelSupplierRefundCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Refund with { Amount = TravelFinanceRules.Round(request.Refund.Amount) };
        // Checked after the bill / payment lookup (scope), before any balance check the first write changed.
        async Task<ApiResponse<int>?> ReplayAsync()
            => await repository.FindByIdempotencyKeyAsync(tenantId, TravelIdempotentRecord.SupplierRefund, dto.IdempotencyKey, cancellationToken) is int id
                ? ApiResponse<int>.SuccessResponse(id, "This refund was already recorded.") : null;
        int supplierId;
        string currency;
        if (dto.BillId is int billId)
        {
            var bill = await guard.GetBillAsync(billId, tenantId, cancellationToken);
            if (await ReplayAsync() is { } replayed) return replayed;
            if (bill.Status != "Posted") return ApiResponse<int>.FailResponse("Refunds are received against posted bills.");
            if (dto.Amount > -bill.Balance)
                return ApiResponse<int>.FailResponse(bill.Balance < 0 ? $"The supplier owes back only {-bill.Balance:0.##} {bill.CurrencyCode} on this bill."
                    : "The supplier owes nothing back on this bill — record a supplier credit first.");
            (supplierId, currency) = (bill.SupplierId, bill.CurrencyCode);
        }
        else
        {
            var payment = await guard.GetPaymentAsync(dto.PaymentId!.Value, tenantId, cancellationToken);
            if (await ReplayAsync() is { } replayed) return replayed;
            if (payment.Status != "Completed") return ApiResponse<int>.FailResponse("Only a cleared payment's advance can come back.");
            if (dto.Amount > payment.Unallocated) return ApiResponse<int>.FailResponse($"Only {payment.Unallocated:0.##} {payment.CurrencyCode} of this payment is unallocated.");
            (supplierId, currency) = (payment.SupplierId, payment.CurrencyCode);
        }
        var (error, advance) = await SupplierAllocations.CheckCancellationAsync(repository, tenantId, dto.CancellationId, supplierId, currency, cancellationToken);
        if (error is not null) return ApiResponse<int>.FailResponse(error);
        var result = await repository.RecordRefundAsync(tenantId, dto, supplierId, currency, currentUser.UserId, DateTime.UtcNow, advance, cancellationToken);
        if (result.Error is not null) return ApiResponse<int>.FailResponse(result.Error);
        return ApiResponse<int>.SuccessResponse(result.Id, result.Replayed ? "This refund was already recorded." : "Supplier refund recorded.");
    }
}
