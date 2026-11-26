using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.SupplierFinance.Commands;

/// <summary>Visibility checks for supplier bills and payments (other tenants / branches → NotFound).</summary>
public sealed class TravelSupplierFinanceGuard(ITravelSupplierBillRepository bills, ITravelSupplierPaymentRepository payments, ITravelBranchScopeResolver scopeResolver)
{
    public const string ConflictMessage = "The record was changed by someone else. Reload and try again.";

    public async Task<TravelSupplierBillState> GetBillAsync(int id, int tenantId, CancellationToken ct)
        => await bills.GetStateAsync(id, tenantId, await scopeResolver.ResolveAsync(tenantId, ct), ct) ?? throw new NotFoundException("TravelSupplierBill", id);

    public async Task<TravelSupplierPaymentState> GetPaymentAsync(int id, int tenantId, CancellationToken ct)
        => await payments.GetStateAsync(id, tenantId, await scopeResolver.ResolveAsync(tenantId, ct), ct) ?? throw new NotFoundException("TravelSupplierPayment", id);
}

public class SaveTravelSupplierBillLineValidator : AbstractValidator<SaveTravelSupplierBillLineDto>
{
    public SaveTravelSupplierBillLineValidator()
    {
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).LessThan(1_000_000_000_000m);
        RuleFor(x => x.CommissionAmount).GreaterThanOrEqualTo(0);
    }
}

internal static class BillLines
{
    /// <summary>Manual lines tagged to a booking / visa case / group must point at this tenant's records.</summary>
    public static async Task<string?> CheckContextsAsync(ITravelSupplierBillRepository repository, int tenantId, IEnumerable<SaveTravelSupplierBillLineDto> lines,
        CancellationToken ct)
    {
        foreach (var l in lines.Where(l => l.SourceType is null && l.ContextType is not null && l.ContextId is > 0))
            if (!await repository.ContextExistsAsync(tenantId, l.ContextType!, l.ContextId!.Value, ct))
                return $"The {l.ContextType} referenced by \"{l.Description}\" was not found.";
        return null;
    }
}

// ── Create / update ─────────────────────────────────────────────────────

public record CreateTravelSupplierBillCommand(CreateTravelSupplierBillDto Bill) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CreateSupplierBill";
    public string AuditEntityName => "TravelSupplierBill";
    public int? AuditEntityId => null;
}

public class CreateTravelSupplierBillCommandValidator : AbstractValidator<CreateTravelSupplierBillCommand>
{
    public CreateTravelSupplierBillCommandValidator()
    {
        RuleFor(x => x.Bill.SupplierId).GreaterThan(0);
        RuleFor(x => x.Bill.CurrencyCode).Must(c => TravelValues.IsCurrencyCode(c?.ToUpperInvariant())).WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.Bill.SupplierReference).MaximumLength(60);
        RuleFor(x => x.Bill.Notes).MaximumLength(2000);
        RuleFor(x => x.Bill.Lines).NotEmpty().Must(l => l.Count <= 300);
        RuleForEach(x => x.Bill.Lines).SetValidator(new SaveTravelSupplierBillLineValidator());
        RuleFor(x => x.Bill.DueDate).GreaterThanOrEqualTo(x => x.Bill.BillDate).When(x => x.Bill.BillDate.HasValue && x.Bill.DueDate.HasValue);
    }
}

public class CreateTravelSupplierBillCommandHandler(ITravelSupplierBillRepository repository, ITravelSupplierRepository suppliers, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<CreateTravelSupplierBillCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelSupplierBillCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Bill;
        _ = await suppliers.GetByIdAsync(dto.SupplierId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelSupplier", dto.SupplierId);
        var currency = dto.CurrencyCode.ToUpperInvariant();
        var reference = string.IsNullOrWhiteSpace(dto.SupplierReference) ? null : dto.SupplierReference.Trim();
        if (reference is not null && await repository.SupplierReferenceInUseAsync(tenantId, dto.SupplierId, reference, null, cancellationToken))
            return ApiResponse<int>.FailResponse($"Supplier invoice {reference} is already entered.");

        var billable = await repository.GetBillableCostsAsync(tenantId, dto.SupplierId, currency, null, cancellationToken);
        var (plan, error) = TravelSupplierFinanceRules.Build(dto.Lines, billable, currency);
        if (error is not null) return ApiResponse<int>.FailResponse(error);
        var contextError = await BillLines.CheckContextsAsync(repository, tenantId, dto.Lines, cancellationToken);
        if (contextError is not null) return ApiResponse<int>.FailResponse(contextError);

        var header = new TravelSupplierBillHeaderWrite(dto.SupplierId, currency, reference, dto.BillDate?.Date, dto.DueDate?.Date, plan!.Gross, plan.Commission,
            plan.Net, string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(), null);
        var id = await repository.CreateAsync(tenantId, header, plan.Lines, currentUser.UserId, cancellationToken);
        if (id == 0) return ApiResponse<int>.FailResponse("A service or the supplier reference was billed by someone else meanwhile. Reload and try again.");
        return ApiResponse<int>.SuccessResponse(id, "Draft bill saved.");
    }
}

public record UpdateTravelSupplierBillCommand(int Id, UpdateTravelSupplierBillDto Bill) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateSupplierBill";
    public string AuditEntityName => "TravelSupplierBill";
    public int? AuditEntityId => Id;
}

public class UpdateTravelSupplierBillCommandValidator : AbstractValidator<UpdateTravelSupplierBillCommand>
{
    public UpdateTravelSupplierBillCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Bill.SupplierReference).MaximumLength(60);
        RuleFor(x => x.Bill.Notes).MaximumLength(2000);
        RuleFor(x => x.Bill.Lines).NotEmpty().Must(l => l.Count <= 300);
        RuleForEach(x => x.Bill.Lines).SetValidator(new SaveTravelSupplierBillLineValidator());
        RuleFor(x => x.Bill.DueDate).GreaterThanOrEqualTo(x => x.Bill.BillDate).When(x => x.Bill.BillDate.HasValue && x.Bill.DueDate.HasValue);
    }
}

public class UpdateTravelSupplierBillCommandHandler(ITravelSupplierBillRepository repository, TravelSupplierFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateTravelSupplierBillCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelSupplierBillCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetBillAsync(request.Id, tenantId, cancellationToken);
        if (state.Status != "Draft") return ApiResponse<bool>.FailResponse("Posted bills cannot be edited — record a supplier credit instead.");
        var dto = request.Bill;
        var reference = string.IsNullOrWhiteSpace(dto.SupplierReference) ? null : dto.SupplierReference.Trim();
        if (reference is not null && await repository.SupplierReferenceInUseAsync(tenantId, state.SupplierId, reference, state.Id, cancellationToken))
            return ApiResponse<bool>.FailResponse($"Supplier invoice {reference} is already entered.");

        var billable = await repository.GetBillableCostsAsync(tenantId, state.SupplierId, state.CurrencyCode, state.Id, cancellationToken);
        var (plan, error) = TravelSupplierFinanceRules.Build(dto.Lines, billable, state.CurrencyCode);
        if (error is not null) return ApiResponse<bool>.FailResponse(error);
        var contextError = await BillLines.CheckContextsAsync(repository, tenantId, dto.Lines, cancellationToken);
        if (contextError is not null) return ApiResponse<bool>.FailResponse(contextError);

        var header = new TravelSupplierBillHeaderWrite(state.SupplierId, state.CurrencyCode, reference, dto.BillDate?.Date, dto.DueDate?.Date, plan!.Gross,
            plan.Commission, plan.Net, string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(), state.BranchId);
        if (!await repository.UpdateDraftAsync(tenantId, state.Id, header, plan.Lines, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelSupplierFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Draft bill saved.");
    }
}

// ── Post / void / delete ────────────────────────────────────────────────

public record PostTravelSupplierBillCommand(int Id, PostTravelSupplierBillDto Post) : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => "PostSupplierBill";
    public string AuditEntityName => "TravelSupplierBill";
    public int? AuditEntityId => Id;
}

public class PostTravelSupplierBillCommandHandler(ITravelSupplierBillRepository repository, ITravelConfigRepository config, TravelSupplierFinanceGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<PostTravelSupplierBillCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(PostTravelSupplierBillCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetBillAsync(request.Id, tenantId, cancellationToken);
        if (state.Status != "Draft") return ApiResponse<string>.FailResponse("Only a draft can be posted.");
        if (state.Total <= 0) return ApiResponse<string>.FailResponse("A bill needs a net amount above zero.");
        var now = DateTime.UtcNow;
        var billDate = (request.Post?.BillDate ?? state.BillDate ?? now).Date;
        var terms = (await config.GetSettingsAsync(tenantId, cancellationToken)).DefaultPaymentTermsDays;
        var dueDate = (request.Post?.DueDate ?? state.DueDate ?? billDate.AddDays(terms)).Date;
        if (dueDate < billDate) return ApiResponse<string>.FailResponse("Due date cannot be before the bill date.");
        var number = await repository.PostAsync(tenantId, state.Id, billDate, dueDate, currentUser.UserId, now, cancellationToken)
                     ?? throw new ConflictException(TravelSupplierFinanceGuard.ConflictMessage);
        return ApiResponse<string>.SuccessResponse(number, $"Bill {number} posted.");
    }
}

public record VoidTravelSupplierBillCommand(int Id, string Reason) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "VoidSupplierBill";
    public string AuditEntityName => "TravelSupplierBill";
    public int? AuditEntityId => Id;
}

public class VoidTravelSupplierBillCommandValidator : AbstractValidator<VoidTravelSupplierBillCommand>
{
    public VoidTravelSupplierBillCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class VoidTravelSupplierBillCommandHandler(ITravelSupplierBillRepository repository, TravelSupplierFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<VoidTravelSupplierBillCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(VoidTravelSupplierBillCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetBillAsync(request.Id, tenantId, cancellationToken);
        if (state.Status != "Posted") return ApiResponse<bool>.FailResponse("Only a posted bill can be voided (delete a draft instead).");
        if (!TravelSupplierBillStateMachine.CanVoid(state)) return ApiResponse<bool>.FailResponse("The bill has payments, credits or refunds — record a supplier credit instead.");
        if (!await repository.VoidAsync(tenantId, state.Id, request.Reason.Trim(), currentUser.UserId, DateTime.UtcNow, cancellationToken))
            return ApiResponse<bool>.FailResponse("The bill received a payment or credit meanwhile and cannot be voided.");
        return ApiResponse<bool>.SuccessResponse(true, "Bill voided; its services can be billed again.");
    }
}

public record DeleteTravelSupplierBillCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteDraftSupplierBill";
    public string AuditEntityName => "TravelSupplierBill";
    public int? AuditEntityId => Id;
}

public class DeleteTravelSupplierBillCommandHandler(ITravelSupplierBillRepository repository, TravelSupplierFinanceGuard guard, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelSupplierBillCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelSupplierBillCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var state = await guard.GetBillAsync(request.Id, tenantId, cancellationToken);
        if (state.Status != "Draft") return ApiResponse<bool>.FailResponse("Only drafts can be deleted — void a posted bill.");
        if (!await repository.DeleteDraftAsync(tenantId, state.Id, cancellationToken)) throw new ConflictException(TravelSupplierFinanceGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Draft deleted.");
    }
}
