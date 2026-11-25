using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Queries;

public class GetTravelEnquiriesQuery : PagedRequest, IRequest<ApiResponse<PagedResult<TravelEnquiryListItemDto>>>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? ServiceType { get; set; }
    public int? AgentUserId { get; set; }
    public int? BranchId { get; set; }
    public int? CustomerId { get; set; }
    /// <summary>Created on or after this date (UTC, inclusive).</summary>
    public DateTime? CreatedFrom { get; set; }
    /// <summary>Created on or before this date (UTC, whole day inclusive).</summary>
    public DateTime? CreatedTo { get; set; }
    /// <summary>Only open enquiries whose next follow-up is due now or overdue.</summary>
    public bool FollowUpDue { get; set; }
}

public class GetTravelEnquiriesQueryValidator : AbstractValidator<GetTravelEnquiriesQuery>
{
    public GetTravelEnquiriesQueryValidator()
    {
        RuleFor(x => x.CreatedTo).GreaterThanOrEqualTo(x => x.CreatedFrom)
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue).WithMessage("'To' must be on or after 'From'.");
        RuleFor(x => x.Status).Must(s => TravelValues.IsOneOf(s, TravelValues.EnquiryStatuses))
            .When(x => !string.IsNullOrEmpty(x.Status)).WithMessage("Unknown status.");
        RuleFor(x => x.ServiceType).Must(s => TravelValues.IsOneOf(s, TravelValues.EnquiryServiceTypes))
            .When(x => !string.IsNullOrEmpty(x.ServiceType)).WithMessage("Unknown service type.");
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

public class GetTravelEnquiriesQueryHandler(
    ITravelEnquiryRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelEnquiriesQuery, ApiResponse<PagedResult<TravelEnquiryListItemDto>>>
{
    public async Task<ApiResponse<PagedResult<TravelEnquiryListItemDto>>> Handle(GetTravelEnquiriesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var filter = new TravelEnquiryListFilter(
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            string.IsNullOrEmpty(request.Status) ? null : request.Status,
            string.IsNullOrEmpty(request.ServiceType) ? null : request.ServiceType,
            request.AgentUserId, request.BranchId, request.CustomerId,
            request.CreatedFrom, request.CreatedTo, request.FollowUpDue, DateTime.UtcNow);
        var result = await repository.GetPagedAsync(tenantId, scope, filter, request.Page, request.PageSize, cancellationToken);
        return ApiResponse<PagedResult<TravelEnquiryListItemDto>>.SuccessResponse(result);
    }
}
