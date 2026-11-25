using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Queries;

public record GetTravelEnquiryByIdQuery(int Id) : IRequest<ApiResponse<TravelEnquiryDto>>;

public class GetTravelEnquiryByIdQueryHandler(
    ITravelEnquiryRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelEnquiryByIdQuery, ApiResponse<TravelEnquiryDto>>
{
    public async Task<ApiResponse<TravelEnquiryDto>> Handle(GetTravelEnquiryByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var enquiry = await repository.GetByIdAsync(request.Id, tenantId, scope, cancellationToken)
            ?? throw new NotFoundException("TravelEnquiry", request.Id);

        enquiry.Activities = await repository.GetActivitiesAsync(request.Id, tenantId, cancellationToken);
        enquiry.AllowedActions = EnquiryStateMachine.AllowedActions(enquiry.Status);
        return ApiResponse<TravelEnquiryDto>.SuccessResponse(enquiry);
    }
}
