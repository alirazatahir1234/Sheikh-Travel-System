using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Queries;

public class GetTravelVisaCasesQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelVisaCaseListItemDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? CountryCode { get; set; }
    public int? VisaTypeId { get; set; }
    public int? AgentUserId { get; set; }
    public int? CustomerId { get; set; }
    /// <summary>Created on or after this date (UTC, inclusive).</summary>
    public DateTime? CreatedFrom { get; set; }
    /// <summary>Created on or before this date (UTC, whole day inclusive).</summary>
    public DateTime? CreatedTo { get; set; }
}

public class GetTravelVisaCasesQueryValidator : AbstractValidator<GetTravelVisaCasesQuery>
{
    public GetTravelVisaCasesQueryValidator()
    {
        RuleFor(x => x.CreatedTo).GreaterThanOrEqualTo(x => x.CreatedFrom)
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue).WithMessage("'To' must be on or after 'From'.");
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.VisaCaseStatuses))
            .When(x => !string.IsNullOrEmpty(x.Status)).WithMessage("Unknown status.");
        RuleFor(x => x.CountryCode).Must(TravelValues.IsCountryCode).When(x => !string.IsNullOrEmpty(x.CountryCode));
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class GetTravelVisaCasesQueryHandler(
    ITravelVisaCaseRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelVisaCasesQuery, ApiResponse<PagedResult<TravelVisaCaseListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelVisaCaseListItemDto>>> Handle(GetTravelVisaCasesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var filter = new TravelVisaCaseListFilter(
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrEmpty(request.Status) ? null : request.Status,
            string.IsNullOrEmpty(request.CountryCode) ? null : request.CountryCode,
            request.VisaTypeId, request.AgentUserId, request.CustomerId, request.CreatedFrom, request.CreatedTo);
        var result = await repository.GetPagedAsync(tenantId, scope, filter, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PagedResult<TravelVisaCaseListItemDto>>.SuccessResponse(result);
    }
}
