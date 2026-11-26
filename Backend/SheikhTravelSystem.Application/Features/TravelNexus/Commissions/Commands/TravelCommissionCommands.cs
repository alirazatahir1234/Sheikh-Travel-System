using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Finance.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Commissions.Commands;

internal static class Beneficiaries
{
    /// <summary>The staff user / sub-agent must belong to this tenant.</summary>
    public static async Task EnsureAsync(ITravelCommissionRepository repository, int tenantId, string type, int id, CancellationToken ct)
    {
        var exists = type == "Staff" ? await repository.UserExistsAsync(tenantId, id, ct) : await repository.SubAgentExistsAsync(tenantId, id, ct);
        if (!exists) throw new NotFoundException(type == "Staff" ? "User" : "TravelSubAgent", id);
    }
}

// ── Sub-agents ──────────────────────────────────────────────────────────

public record SaveTravelSubAgentCommand(int? Id, SaveTravelSubAgentDto SubAgent) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id is null ? "CreateSubAgent" : "UpdateSubAgent";
    public string AuditEntityName => "TravelSubAgent";
    public int? AuditEntityId => Id;
}

public class SaveTravelSubAgentCommandValidator : AbstractValidator<SaveTravelSubAgentCommand>
{
    public SaveTravelSubAgentCommandValidator()
    {
        RuleFor(x => x.SubAgent.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.SubAgent.Phone).MaximumLength(30);
        RuleFor(x => x.SubAgent.Email).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.SubAgent.Email));
        RuleFor(x => x.SubAgent.Notes).MaximumLength(1000);
    }
}

public class SaveTravelSubAgentCommandHandler(ITravelCommissionRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelSubAgentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelSubAgentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var s = request.SubAgent;
        var dto = s with
        {
            Name = s.Name.Trim(), Phone = string.IsNullOrWhiteSpace(s.Phone) ? null : s.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(s.Email) ? null : s.Email.Trim(), Notes = string.IsNullOrWhiteSpace(s.Notes) ? null : s.Notes.Trim()
        };
        if (await repository.SubAgentNameExistsAsync(tenantId, dto.Name, request.Id, cancellationToken))
            return ApiResponse<int>.FailResponse($"A sub-agent named \"{dto.Name}\" already exists.");
        var id = await repository.SaveSubAgentAsync(tenantId, request.Id, dto, currentUser.UserId, cancellationToken);
        if (id == 0) throw new NotFoundException("TravelSubAgent", request.Id ?? 0);
        return ApiResponse<int>.SuccessResponse(id, request.Id is null ? "Sub-agent added." : "Sub-agent updated.");
    }
}

public record DeleteTravelSubAgentCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteSubAgent";
    public string AuditEntityName => "TravelSubAgent";
    public int? AuditEntityId => Id;
}

public class DeleteTravelSubAgentCommandHandler(ITravelCommissionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelSubAgentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelSubAgentCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteSubAgentAsync(tenantContext.GetRequiredTenantId(), request.Id, cancellationToken))
            throw new NotFoundException("TravelSubAgent", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Sub-agent removed.");
    }
}

// ── Commission rules ────────────────────────────────────────────────────

public record SaveTravelCommissionRuleCommand(int? Id, SaveTravelCommissionRuleDto Rule) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id is null ? "CreateCommissionRule" : "UpdateCommissionRule";
    public string AuditEntityName => "TravelCommissionRule";
    public int? AuditEntityId => Id;
}

public class SaveTravelCommissionRuleCommandValidator : AbstractValidator<SaveTravelCommissionRuleCommand>
{
    public SaveTravelCommissionRuleCommandValidator()
    {
        RuleFor(x => x.Rule.BeneficiaryType).Must(t => TravelValues.IsOneOf(t, TravelValues.BeneficiaryTypes)).WithMessage("Unknown beneficiary type.");
        RuleFor(x => x.Rule.SubAgentId).Null().When(x => x.Rule.BeneficiaryType == "Staff").WithMessage("A staff rule cannot name a sub-agent.");
        RuleFor(x => x.Rule.UserId).Null().When(x => x.Rule.BeneficiaryType == "SubAgent").WithMessage("A sub-agent rule cannot name a staff user.");
        RuleFor(x => x.Rule.Scope).Must(s => TravelValues.IsOneOf(s, TravelValues.CommissionScopes)).WithMessage("Unknown scope.");
        RuleFor(x => x.Rule.Basis).Must(b => TravelValues.IsOneOf(b, TravelValues.CommissionBases)).WithMessage("Unknown basis.");
        RuleFor(x => x.Rule.Rate).GreaterThan(0).LessThanOrEqualTo(100).WithMessage("The rate is a percentage between 0 and 100.");
        RuleFor(x => x.Rule.ValidTo).Must((x, to) => to is null || to.Value.Date >= x.Rule.ValidFrom.Date).WithMessage("Valid-to must not be before valid-from.");
    }
}

public class SaveTravelCommissionRuleCommandHandler(ITravelCommissionRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelCommissionRuleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelCommissionRuleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var rule = request.Rule with { ValidFrom = request.Rule.ValidFrom.Date, ValidTo = request.Rule.ValidTo?.Date };
        if (rule.UserId is int userId) await Beneficiaries.EnsureAsync(repository, tenantId, "Staff", userId, cancellationToken);
        if (rule.SubAgentId is int subAgentId) await Beneficiaries.EnsureAsync(repository, tenantId, "SubAgent", subAgentId, cancellationToken);
        var id = await repository.SaveRuleAsync(tenantId, request.Id, rule, currentUser.UserId, cancellationToken);
        if (id == 0) throw new NotFoundException("TravelCommissionRule", request.Id ?? 0);
        return ApiResponse<int>.SuccessResponse(id, request.Id is null ? "Commission rule added." : "Commission rule updated.");
    }
}

public record DeleteTravelCommissionRuleCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteCommissionRule";
    public string AuditEntityName => "TravelCommissionRule";
    public int? AuditEntityId => Id;
}

public class DeleteTravelCommissionRuleCommandHandler(ITravelCommissionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelCommissionRuleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelCommissionRuleCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteRuleAsync(tenantContext.GetRequiredTenantId(), request.Id, cancellationToken))
            throw new NotFoundException("TravelCommissionRule", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Commission rule removed.");
    }
}

// ── Payouts and attribution ─────────────────────────────────────────────

public record RecordTravelCommissionPayoutCommand(RecordTravelCommissionPayoutDto Payout) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "RecordCommissionPayout";
    public string AuditEntityName => "TravelCommissionPayout";
    public int? AuditEntityId => null;
}

public class RecordTravelCommissionPayoutCommandValidator : AbstractValidator<RecordTravelCommissionPayoutCommand>
{
    public RecordTravelCommissionPayoutCommandValidator()
    {
        RuleFor(x => x.Payout.IdempotencyKey).NotEqual(Guid.Empty).WithMessage("A client request key is required.");
        RuleFor(x => x.Payout.BeneficiaryType).Must(t => TravelValues.IsOneOf(t, TravelValues.BeneficiaryTypes)).WithMessage("Unknown beneficiary type.");
        RuleFor(x => x.Payout.BeneficiaryId).GreaterThan(0);
        RuleFor(x => x.Payout.Amount).GreaterThan(0).LessThan(1_000_000_000_000m);
        RuleFor(x => x.Payout.CurrencyCode).Must(c => TravelValues.IsCurrencyCode(c?.ToUpperInvariant())).WithMessage("Currency must be a 3-letter ISO code.");
        RuleFor(x => x.Payout.Method).Must(m => TravelValues.IsOneOf(m, TravelValues.PaymentMethods)).WithMessage("Unknown payment method.");
        RuleFor(x => x.Payout.PaidOn).Must(d => d.Date <= DateTime.UtcNow.Date.AddDays(1)).WithMessage("A payout cannot be made in the future.");
        RuleFor(x => x.Payout.Reference).MaximumLength(100);
        RuleFor(x => x.Payout.Notes).MaximumLength(1000);
    }
}

/// <summary>Pays commission out — never more than the beneficiary's payable balance in that currency.</summary>
public class RecordTravelCommissionPayoutCommandHandler(ITravelCommissionRepository repository, TravelProfitabilityService profitability,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<RecordTravelCommissionPayoutCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RecordTravelCommissionPayoutCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var p = request.Payout;
        await Beneficiaries.EnsureAsync(repository, tenantId, p.BeneficiaryType, p.BeneficiaryId, cancellationToken);
        var dto = p with
        {
            Amount = Finance.TravelFinanceRules.Round(p.Amount), CurrencyCode = p.CurrencyCode.ToUpperInvariant(), PaidOn = p.PaidOn.Date,
            Reference = string.IsNullOrWhiteSpace(p.Reference) ? null : p.Reference.Trim(), Notes = string.IsNullOrWhiteSpace(p.Notes) ? null : p.Notes.Trim()
        };
        var fx = await profitability.FxAsync(tenantId, cancellationToken);
        if (fx is null) return ApiResponse<int>.FailResponse("Set the reporting currency in Travel settings first.");
        var (_, lines) = await profitability.CommissionLinesAsync(tenantId, fx, dto.BeneficiaryType, dto.BeneficiaryId, null, null, cancellationToken);
        var payouts = await repository.GetPayoutsAsync(tenantId, dto.BeneficiaryType, dto.BeneficiaryId, cancellationToken);
        var balance = TravelCommissionEngine.Balances(lines, payouts).FirstOrDefault(b => b.CurrencyCode == dto.CurrencyCode)?.Balance ?? 0;
        if (dto.Amount > balance)
            return ApiResponse<int>.FailResponse(balance > 0 ? $"Only {balance:0.##} {dto.CurrencyCode} is payable to this beneficiary."
                : $"Nothing is payable to this beneficiary in {dto.CurrencyCode}.");
        var result = await repository.RecordPayoutAsync(tenantId, dto, currentUser.UserId, DateTime.UtcNow, cancellationToken);
        if (result.Error is not null) return ApiResponse<int>.FailResponse(result.Error);
        return ApiResponse<int>.SuccessResponse(result.Id, result.Replayed ? "This payout was already recorded." : "Commission payout recorded.");
    }
}

public record UpdateTravelInvoiceAttributionCommand(int InvoiceId, UpdateTravelInvoiceAttributionDto Attribution) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateInvoiceAttribution";
    public string AuditEntityName => "TravelInvoice";
    public int? AuditEntityId => InvoiceId;
}

public class UpdateTravelInvoiceAttributionCommandHandler(ITravelCommissionRepository repository, TravelFinanceGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<UpdateTravelInvoiceAttributionCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelInvoiceAttributionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var invoice = await guard.GetInvoiceAsync(request.InvoiceId, tenantId, cancellationToken);
        if (invoice.Status == "Void") return ApiResponse<bool>.FailResponse("A void invoice earns no commission.");
        var a = request.Attribution;
        if (a.AgentUserId is int userId) await Beneficiaries.EnsureAsync(repository, tenantId, "Staff", userId, cancellationToken);
        if (a.SubAgentId is int subAgentId) await Beneficiaries.EnsureAsync(repository, tenantId, "SubAgent", subAgentId, cancellationToken);
        if (!await repository.UpdateAttributionAsync(tenantId, invoice.Id, a.AgentUserId, a.SubAgentId, currentUser.UserId, cancellationToken))
            throw new NotFoundException("TravelInvoice", request.InvoiceId);
        return ApiResponse<bool>.SuccessResponse(true, "Commission attribution updated.");
    }
}
