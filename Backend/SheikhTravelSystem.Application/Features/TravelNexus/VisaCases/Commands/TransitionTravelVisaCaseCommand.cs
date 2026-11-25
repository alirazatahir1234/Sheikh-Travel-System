using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;

/// <summary>
/// Pipeline moves other than Submit and decisions: StartDocuments, VerifyDocuments, ReopenDocuments,
/// StartProcessing, Hold, Resume, Cancel (Cancel requires a note = reason).
/// </summary>
public record TransitionTravelVisaCaseCommand(int Id, TransitionTravelVisaCaseDto Transition)
    : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => Transition.Action;
    public string AuditEntityName => "TravelVisaCase";
    public int? AuditEntityId => Id;
}

public class TransitionTravelVisaCaseCommandValidator : AbstractValidator<TransitionTravelVisaCaseCommand>
{
    public TransitionTravelVisaCaseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Transition).NotNull();
        RuleFor(x => x.Transition.Action).Must(a => TravelValues.IsOneOf(a, VisaCaseStateMachine.TransitionActions))
            .WithMessage("Unknown action.");
        RuleFor(x => x.Transition.Note).NotEmpty().When(x => x.Transition.Action == VisaCaseStateMachine.Actions.Cancel)
            .WithMessage("Give a reason for cancelling.");
        RuleFor(x => x.Transition.Note).MaximumLength(500);
    }
}

public class TransitionTravelVisaCaseCommandHandler(
    ITravelVisaCaseRepository repository,
    TravelVisaCaseGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<TransitionTravelVisaCaseCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(TransitionTravelVisaCaseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);
        var action = request.Transition.Action;

        var target = VisaCaseStateMachine.Target(state.Status, action, state.OnHoldFromStatus);
        if (target is null)
            return ApiResponse<string>.FailResponse($"Cannot {action} a case that is {state.Status}.");

        if (action == VisaCaseStateMachine.Actions.VerifyDocuments
            && await guard.ActiveApplicantCountAsync(request.Id, tenantId, cancellationToken) == 0)
            return ApiResponse<string>.FailResponse("Add at least one applicant before verifying documents.");

        var note = string.IsNullOrWhiteSpace(request.Transition.Note) ? null : request.Transition.Note.Trim();
        var change = new TravelVisaCaseStatusChange(
            request.Id, state.Status, target,
            NewOnHoldFromStatus: target == "OnHold" ? state.Status : null,
            Note: note,
            CancelReason: target == "Cancelled" ? note : null);

        if (!await repository.ChangeStatusAsync(tenantId, change, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelVisaCaseRules.ConflictMessage);

        return ApiResponse<string>.SuccessResponse(target, $"Case is now {target}.");
    }
}
