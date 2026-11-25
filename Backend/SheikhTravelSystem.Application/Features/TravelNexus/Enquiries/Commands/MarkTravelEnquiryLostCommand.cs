using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;

public record MarkTravelEnquiryLostCommand(int Id, MarkTravelEnquiryLostDto Lost) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "MarkLost";
    public string AuditEntityName => "TravelEnquiry";
    public int? AuditEntityId => Id;
}

public class MarkTravelEnquiryLostCommandValidator : AbstractValidator<MarkTravelEnquiryLostCommand>
{
    public MarkTravelEnquiryLostCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Lost).NotNull();
        RuleFor(x => x.Lost.LostReasonId).GreaterThan(0).WithMessage("Choose why the enquiry was lost.");
        RuleFor(x => x.Lost.LostNotes).MaximumLength(1000);
    }
}

public class MarkTravelEnquiryLostCommandHandler(
    ITravelEnquiryRepository repository,
    ITravelConfigRepository config,
    TravelEnquiryGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<MarkTravelEnquiryLostCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(MarkTravelEnquiryLostCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var enquiry = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);

        if (EnquiryStateMachine.Target(enquiry.Status, EnquiryStateMachine.Actions.MarkLost) is null)
            return ApiResponse<bool>.FailResponse($"A {enquiry.Status} enquiry cannot be marked lost.");

        var reason = await config.GetLostReasonByIdAsync(request.Lost.LostReasonId, tenantId, cancellationToken);
        if (reason is null) return ApiResponse<bool>.FailResponse("Lost reason not found.");

        var notes = string.IsNullOrWhiteSpace(request.Lost.LostNotes) ? null : request.Lost.LostNotes.Trim();
        var change = new TravelEnquiryStatusChange(request.Id, enquiry.Status, "Lost",
            $"Status {enquiry.Status} → Lost: {reason.Name}" + (notes is null ? "" : $" ({notes})"), reason.Id, notes);
        if (!await repository.ChangeStatusAsync(tenantId, change, currentUser.UserId, cancellationToken))
            throw new ConflictException("The enquiry was changed by someone else. Reload and try again.");

        return ApiResponse<bool>.SuccessResponse(true, "Enquiry marked as lost.");
    }
}
