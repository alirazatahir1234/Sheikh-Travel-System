using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Profitability.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Profitability.Queries;

/// <summary>
/// Profit by booking file, visa case or group whose travel date falls in the range — or rolled up by agent / branch over all three.
/// Both dates are required; an inverted range is empty.
/// </summary>
public record GetTravelProfitabilityQuery(string Dimension, DateTime? TravelFrom, DateTime? TravelTo, int? BranchId, int? AgentUserId)
    : IRequest<ApiResponse<TravelProfitabilityReportDto>>;

public class GetTravelProfitabilityQueryValidator : AbstractValidator<GetTravelProfitabilityQuery>
{
    public static readonly string[] Dimensions = ["Booking", "VisaCase", "Group", "Agent", "Branch"];

    public GetTravelProfitabilityQueryValidator()
    {
        RuleFor(x => x.Dimension).Must(d => TravelValues.IsOneOf(d, Dimensions)).WithMessage("Unknown dimension.");
        RuleFor(x => x.TravelFrom).NotNull().WithMessage("Choose a travel date range.");
        RuleFor(x => x.TravelTo).NotNull().WithMessage("Choose a travel date range.");
        RuleFor(x => x).Must(x => x.TravelFrom is null || x.TravelTo is null || (x.TravelTo.Value - x.TravelFrom.Value).TotalDays <= 731)
            .WithMessage("The range can be at most two years.");
    }
}

public class GetTravelProfitabilityQueryHandler(ITravelProfitabilityRepository facts, TravelProfitabilityService service, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext) : IRequestHandler<GetTravelProfitabilityQuery, ApiResponse<TravelProfitabilityReportDto>>
{
    public async Task<ApiResponse<TravelProfitabilityReportDto>> Handle(GetTravelProfitabilityQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var fx = await service.FxAsync(tenantId, cancellationToken);
        if (fx is null) return ApiResponse<TravelProfitabilityReportDto>.FailResponse("Set the reporting currency in Travel settings first.");
        var report = new TravelProfitabilityReportDto { ReportingCurrencyCode = fx.ReportingCurrency, Dimension = request.Dimension };
        var from = request.TravelFrom!.Value.Date;
        var to = request.TravelTo!.Value.Date;
        if (to < from) return ApiResponse<TravelProfitabilityReportDto>.SuccessResponse(report);

        var types = request.Dimension is "Agent" or "Branch" ? TravelValues.ContextTypes : [request.Dimension];
        var contexts = (await facts.GetContextsAsync(tenantId, await scopeResolver.ResolveAsync(tenantId, cancellationToken), types, from, to, cancellationToken))
            .Where(c => request.BranchId is null || c.BranchId == request.BranchId)
            .Where(c => request.AgentUserId is null || c.AgentUserId == request.AgentUserId)
            .ToList();
        if (contexts.Count == 0) return ApiResponse<TravelProfitabilityReportDto>.SuccessResponse(report);

        var computation = await service.ComputeAsync(tenantId, fx, contexts, cancellationToken);
        var rows = computation.Profit.Contexts.Values.Select(TravelProfitabilityEngine.ToDto)
            .OrderBy(r => r.TravelDate ?? DateTime.MaxValue).ThenBy(r => r.Label, StringComparer.Ordinal).ToList();
        report.Totals = TravelProfitabilityEngine.Sum("Total", "Total", rows);
        report.MissingRates = fx.Missing.Select(m => new TravelMissingRateDto { CurrencyCode = m.Currency, EarliestDate = m.Earliest, Amounts = m.Count }).ToList();
        if (request.Dimension == "Agent")
            report.Groups = rows.GroupBy(r => r.AgentUserId)
                .Select(g => TravelProfitabilityEngine.Sum(g.Key?.ToString() ?? "None", g.First().AgentName ?? "Unassigned", g))
                .OrderByDescending(g => g.Margin).ToList();
        else if (request.Dimension == "Branch")
            report.Groups = rows.GroupBy(r => r.BranchId)
                .Select(g => TravelProfitabilityEngine.Sum(g.Key?.ToString() ?? "None", g.First().BranchName ?? "No branch", g))
                .OrderByDescending(g => g.Margin).ToList();
        else
            report.Contexts = rows;
        return ApiResponse<TravelProfitabilityReportDto>.SuccessResponse(report);
    }
}

/// <summary>Revenue, cost (estimated flags) and commission lines of one booking file, visa case or group.</summary>
public record GetTravelContextProfitQuery(string ContextType, int ContextId) : IRequest<ApiResponse<TravelContextProfitDetailDto>>;

public class GetTravelContextProfitQueryValidator : AbstractValidator<GetTravelContextProfitQuery>
{
    public GetTravelContextProfitQueryValidator()
        => RuleFor(x => x.ContextType).Must(t => TravelValues.IsOneOf(t, TravelValues.ContextTypes)).WithMessage("Unknown context.");
}

public class GetTravelContextProfitQueryHandler(ITravelProfitabilityRepository facts, TravelProfitabilityService service, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext) : IRequestHandler<GetTravelContextProfitQuery, ApiResponse<TravelContextProfitDetailDto>>
{
    public async Task<ApiResponse<TravelContextProfitDetailDto>> Handle(GetTravelContextProfitQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var key = TravelProfitabilityEngine.Key(request.ContextType, request.ContextId);
        var context = (await facts.GetContextsByKeyAsync(tenantId, [key], cancellationToken)).FirstOrDefault();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        if (context is null || !scope.Allows(context.BranchId)) throw new NotFoundException(request.ContextType, request.ContextId);
        var fx = await service.FxAsync(tenantId, cancellationToken);
        if (fx is null) return ApiResponse<TravelContextProfitDetailDto>.FailResponse("Set the reporting currency in Travel settings first.");

        var computation = await service.ComputeAsync(tenantId, fx, [context], cancellationToken);
        var result = computation.Profit.Contexts[key];
        var summary = TravelProfitabilityEngine.ToDto(result);
        var lines = result.Lines.OrderBy(l => l.Kind == "Revenue" ? 0 : 1).ThenBy(l => l.Date).ToList();
        if (result.Commission != 0)
            lines.Add(new TravelProfitLineDto
            {
                Kind = "Commission", Description = "Agent commission", Reference = string.Empty, Date = lines.Count > 0 ? lines.Max(l => l.Date) : DateTime.UtcNow.Date,
                Amount = summary.Commission, CurrencyCode = fx.ReportingCurrency, Converted = summary.Commission
            });
        return ApiResponse<TravelContextProfitDetailDto>.SuccessResponse(new TravelContextProfitDetailDto
        {
            ContextType = summary.ContextType, ContextId = summary.ContextId, Label = summary.Label, TravelDate = summary.TravelDate, BranchId = summary.BranchId,
            BranchName = summary.BranchName, AgentUserId = summary.AgentUserId, AgentName = summary.AgentName, Revenue = summary.Revenue, Cost = summary.Cost,
            EstimatedCost = summary.EstimatedCost, Commission = summary.Commission, MissingRateCount = summary.MissingRateCount,
            ReportingCurrencyCode = fx.ReportingCurrency, Lines = lines
        });
    }
}
