using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;

/// <summary>Simple pipeline moves: MarkContacted, MarkQualified, MarkQuoted, Reopen.</summary>
public record ChangeTravelEnquiryStatusCommand(int Id, string Action, string? Note) : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => Action;
    public string AuditEntityName => "TravelEnquiry";
    public int? AuditEntityId => Id;
}

public class ChangeTravelEnquiryStatusCommandValidator : AbstractValidator<ChangeTravelEnquiryStatusCommand>
{
    public ChangeTravelEnquiryStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Action).Must(a => TravelValues.IsOneOf(a, EnquiryStateMachine.SimpleStatusActions))
            .WithMessage("Unknown action.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class ChangeTravelEnquiryStatusCommandHandler(
    ITravelEnquiryRepository repository,
    TravelEnquiryGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ChangeTravelEnquiryStatusCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(ChangeTravelEnquiryStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var enquiry = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);

        var target = EnquiryStateMachine.Target(enquiry.Status, request.Action);
        if (target is null)
            return ApiResponse<string>.FailResponse($"Cannot {request.Action} an enquiry that is {enquiry.Status}.");

        var summary = $"Status {enquiry.Status} → {target}" + (string.IsNullOrWhiteSpace(request.Note) ? "" : $": {request.Note.Trim()}");
        var change = new TravelEnquiryStatusChange(request.Id, enquiry.Status, target, summary);
        if (!await repository.ChangeStatusAsync(tenantId, change, currentUser.UserId, cancellationToken))
            throw new ConflictException("The enquiry was changed by someone else. Reload and try again.");

        return ApiResponse<string>.SuccessResponse(target, $"Enquiry is now {target}.");
    }
}
