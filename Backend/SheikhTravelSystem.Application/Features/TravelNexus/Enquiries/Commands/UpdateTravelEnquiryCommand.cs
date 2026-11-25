using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;

public record UpdateTravelEnquiryCommand(int Id, SaveTravelEnquiryDto Enquiry) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TravelEnquiry";
    public int? AuditEntityId => Id;
}

public class UpdateTravelEnquiryCommandValidator : AbstractValidator<UpdateTravelEnquiryCommand>
{
    public UpdateTravelEnquiryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Enquiry).NotNull().SetValidator(new SaveTravelEnquiryDtoValidator());
    }
}

public class UpdateTravelEnquiryCommandHandler(
    ITravelEnquiryRepository repository,
    TravelEnquiryGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<UpdateTravelEnquiryCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelEnquiryCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var existing = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);
        if (!EnquiryStateMachine.IsOpen(existing.Status))
            return ApiResponse<bool>.FailResponse($"A {existing.Status} enquiry can no longer be edited.");

        var (dto, error) = await guard.PrepareAsync(tenantId, scope, request.Enquiry, cancellationToken);
        if (error is not null) return ApiResponse<bool>.FailResponse(error);

        if (!await repository.UpdateAsync(request.Id, tenantId, dto!, cancellationToken))
            throw new ConflictException("The enquiry was changed by someone else. Reload and try again.");
        return ApiResponse<bool>.SuccessResponse(true, "Enquiry updated.");
    }
}
