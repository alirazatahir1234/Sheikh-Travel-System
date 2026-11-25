using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;

public record AddTravelVisaApplicantsCommand(int CaseId, AddTravelVisaApplicantsDto Applicants) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "AddApplicants";
    public string AuditEntityName => "TravelVisaCase";
    public int? AuditEntityId => CaseId;
}

public class AddTravelVisaApplicantsCommandValidator : AbstractValidator<AddTravelVisaApplicantsCommand>
{
    public AddTravelVisaApplicantsCommandValidator()
    {
        RuleFor(x => x.CaseId).GreaterThan(0);
        RuleFor(x => x.Applicants).NotNull();
        RuleFor(x => x.Applicants.TravelerIds).NotEmpty().Must(ids => ids.Count <= 100).WithMessage("Choose 1–100 travelers.");
        RuleForEach(x => x.Applicants.TravelerIds).GreaterThan(0);
    }
}

public class AddTravelVisaApplicantsCommandHandler(
    ITravelVisaCaseRepository repository,
    ITravelTravelerRepository travelers,
    ITravelGroupRepository groups,
    TravelVisaCaseGuard guard,
    Documents.TravelDocumentPlanner planner,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<AddTravelVisaApplicantsCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(AddTravelVisaApplicantsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.CaseId, tenantId, scope, cancellationToken);

        if (!VisaCaseStateMachine.IsPreSubmission(state.Status))
            return ApiResponse<int>.FailResponse($"Applicants cannot be changed once a case is {state.Status}.");

        // Customer case: that customer's travelers. Group case: the group's active pilgrims.
        var allowed = state.GroupId is int groupId
            ? (await groups.GetActiveMemberTravelerIdsAsync(groupId, tenantId, cancellationToken)).ToHashSet()
            : (await travelers.GetByCustomerAsync(state.CustomerId!.Value, tenantId, cancellationToken)).Select(t => t.Id).ToHashSet();
        var requested = request.Applicants.TravelerIds.Distinct().ToList();
        if (requested.Any(id => !allowed.Contains(id)))
            return ApiResponse<int>.FailResponse(state.GroupId is null
                ? "Only travelers of this case's customer can be added."
                : "Only active pilgrims of this group can be added.");

        // Once documents have started, new applicants get the case's checklist requirements immediately; a verified
        // case falls back to DocumentsPending when a newcomer brings mandatory documents.
        var documentsStarted = state.Status is "DocumentsPending" or "DocumentsVerified";
        var seeds = documentsStarted
            ? await planner.PlanForNewApplicantsAsync(state, tenantId, requested, cancellationToken)
            : [];
        var revert = state.Status == "DocumentsVerified" && seeds.Any(s => s.IsMandatory);

        var added = await repository.AddApplicantsAsync(tenantId, request.CaseId, state.Status, requested, currentUser.UserId,
                seeds, revert, cancellationToken)
            ?? throw new ConflictException(TravelVisaCaseRules.ConflictMessage);

        return ApiResponse<int>.SuccessResponse(added, added == 0
            ? "Those travelers are already applicants."
            : $"{added} applicant{(added == 1 ? "" : "s")} added.");
    }
}
