using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Commissions.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Commissions.Queries;

public record GetTravelSubAgentsQuery(bool? IsActive) : IRequest<ApiResponse<IReadOnlyList<TravelSubAgentDto>>>;

public class GetTravelSubAgentsQueryHandler(ITravelCommissionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelSubAgentsQuery, ApiResponse<IReadOnlyList<TravelSubAgentDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelSubAgentDto>>> Handle(GetTravelSubAgentsQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelSubAgentDto>>.SuccessResponse(
            await repository.GetSubAgentsAsync(tenantContext.GetRequiredTenantId(), request.IsActive, cancellationToken));
}

public record GetTravelCommissionRulesQuery : IRequest<ApiResponse<IReadOnlyList<TravelCommissionRuleDto>>>;

public class GetTravelCommissionRulesQueryHandler(ITravelCommissionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelCommissionRulesQuery, ApiResponse<IReadOnlyList<TravelCommissionRuleDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelCommissionRuleDto>>> Handle(GetTravelCommissionRulesQuery request, CancellationToken cancellationToken)
        => ApiResponse<IReadOnlyList<TravelCommissionRuleDto>>.SuccessResponse(await repository.GetRulesAsync(tenantContext.GetRequiredTenantId(), cancellationToken));
}

/// <summary>Commission per invoice and beneficiary (derived); issue date range applied, inverted range → empty.</summary>
public record GetTravelCommissionLinesQuery(string? BeneficiaryType, int? BeneficiaryId, string? Status, DateTime? IssuedFrom, DateTime? IssuedTo)
    : IRequest<ApiResponse<IReadOnlyList<TravelCommissionLineDto>>>;

public class GetTravelCommissionLinesQueryValidator : AbstractValidator<GetTravelCommissionLinesQuery>
{
    public GetTravelCommissionLinesQueryValidator()
    {
        RuleFor(x => x.BeneficiaryType).Must(t => TravelValues.IsOneOf(t, TravelValues.BeneficiaryTypes)).When(x => !string.IsNullOrEmpty(x.BeneficiaryType));
        RuleFor(x => x.BeneficiaryType).NotEmpty().When(x => x.BeneficiaryId.HasValue).WithMessage("Say whether the beneficiary is staff or a sub-agent.");
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.CommissionStatuses)).When(x => !string.IsNullOrEmpty(x.Status));
    }
}

public class GetTravelCommissionLinesQueryHandler(TravelProfitabilityService profitability, ITenantContext tenantContext)
    : IRequestHandler<GetTravelCommissionLinesQuery, ApiResponse<IReadOnlyList<TravelCommissionLineDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelCommissionLineDto>>> Handle(GetTravelCommissionLinesQuery request, CancellationToken cancellationToken)
    {
        if (request.IssuedFrom.HasValue && request.IssuedTo.HasValue && request.IssuedTo.Value.Date < request.IssuedFrom.Value.Date)
            return ApiResponse<IReadOnlyList<TravelCommissionLineDto>>.SuccessResponse([]);
        var tenantId = tenantContext.GetRequiredTenantId();
        var fx = await profitability.FxAsync(tenantId, cancellationToken);
        if (fx is null) return ApiResponse<IReadOnlyList<TravelCommissionLineDto>>.FailResponse("Set the reporting currency in Travel settings first.");
        var (_, lines) = await profitability.CommissionLinesAsync(tenantId, fx, NullIfEmpty(request.BeneficiaryType), request.BeneficiaryId,
            request.IssuedFrom?.Date, request.IssuedTo?.Date, cancellationToken);
        var filtered = string.IsNullOrEmpty(request.Status) ? lines : lines.Where(l => l.Status == request.Status).ToList();
        return ApiResponse<IReadOnlyList<TravelCommissionLineDto>>.SuccessResponse(filtered);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}

/// <summary>Accrued / payable / paid / balance per beneficiary and currency (all time).</summary>
public record GetTravelCommissionBalancesQuery(string? BeneficiaryType, int? BeneficiaryId) : IRequest<ApiResponse<IReadOnlyList<TravelCommissionBalanceDto>>>;

public class GetTravelCommissionBalancesQueryHandler(TravelProfitabilityService profitability, ITravelCommissionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelCommissionBalancesQuery, ApiResponse<IReadOnlyList<TravelCommissionBalanceDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelCommissionBalanceDto>>> Handle(GetTravelCommissionBalancesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var fx = await profitability.FxAsync(tenantId, cancellationToken);
        if (fx is null) return ApiResponse<IReadOnlyList<TravelCommissionBalanceDto>>.FailResponse("Set the reporting currency in Travel settings first.");
        var type = string.IsNullOrEmpty(request.BeneficiaryType) ? null : request.BeneficiaryType;
        var id = type is null ? null : request.BeneficiaryId;
        var (_, lines) = await profitability.CommissionLinesAsync(tenantId, fx, type, id, null, null, cancellationToken);
        var payouts = await repository.GetPayoutsAsync(tenantId, type, id, cancellationToken);
        return ApiResponse<IReadOnlyList<TravelCommissionBalanceDto>>.SuccessResponse(TravelCommissionEngine.Balances(lines, payouts));
    }
}

public record GetTravelCommissionPayoutsQuery(string? BeneficiaryType, int? BeneficiaryId) : IRequest<ApiResponse<IReadOnlyList<TravelCommissionPayoutDto>>>;

public class GetTravelCommissionPayoutsQueryHandler(ITravelCommissionRepository repository, ITenantContext tenantContext)
    : IRequestHandler<GetTravelCommissionPayoutsQuery, ApiResponse<IReadOnlyList<TravelCommissionPayoutDto>>>
{
    public async Task<ApiResponse<IReadOnlyList<TravelCommissionPayoutDto>>> Handle(GetTravelCommissionPayoutsQuery request, CancellationToken cancellationToken)
    {
        var type = string.IsNullOrEmpty(request.BeneficiaryType) ? null : request.BeneficiaryType;
        return ApiResponse<IReadOnlyList<TravelCommissionPayoutDto>>.SuccessResponse(await repository.GetPayoutsAsync(tenantContext.GetRequiredTenantId(), type,
            type is null ? null : request.BeneficiaryId, cancellationToken));
    }
}
