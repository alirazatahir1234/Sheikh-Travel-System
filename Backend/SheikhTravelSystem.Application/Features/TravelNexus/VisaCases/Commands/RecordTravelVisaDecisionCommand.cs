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
/// Embassy decision for one applicant (Approved, Rejected or Withdrawn). When the last active applicant is
/// decided, the case outcome (Approved / PartiallyApproved / Rejected) is set in the same transaction.
/// </summary>
public record RecordTravelVisaDecisionCommand(int CaseId, int ApplicantId, RecordTravelVisaDecisionDto Decision)
    : IRequest<ApiResponse<TravelVisaDecisionResult>>, IAuditableCommand
{
    public string AuditAction => "RecordDecision";
    public string AuditEntityName => "TravelVisaCase";
    public int? AuditEntityId => CaseId;
}

public class RecordTravelVisaDecisionCommandValidator : AbstractValidator<RecordTravelVisaDecisionCommand>
{
    private static readonly string[] Recordable = ["Approved", "Rejected", "Withdrawn"];

    public RecordTravelVisaDecisionCommandValidator()
    {
        RuleFor(x => x.CaseId).GreaterThan(0);
        RuleFor(x => x.ApplicantId).GreaterThan(0);
        RuleFor(x => x.Decision).NotNull();
        RuleFor(x => x.Decision.Decision).Must(d => TravelValues.IsOneOf(d, Recordable))
            .WithMessage("Decision must be Approved, Rejected or Withdrawn.");
        RuleFor(x => x.Decision.RejectionReason).NotEmpty().When(x => x.Decision.Decision == "Rejected")
            .WithMessage("Give the rejection reason.");
        RuleFor(x => x.Decision.RejectionReason).MaximumLength(500);
        RuleFor(x => x.Decision.VisaNumber).MaximumLength(50);
        RuleFor(x => x.Decision.VisaExpiryDate).GreaterThan(x => x.Decision.VisaValidFrom)
            .When(x => x.Decision.VisaValidFrom.HasValue && x.Decision.VisaExpiryDate.HasValue)
            .WithMessage("Visa expiry must be after the valid-from date.");
        RuleFor(x => x.Decision.Notes).MaximumLength(1000);
    }
}

public class RecordTravelVisaDecisionCommandHandler(
    ITravelVisaCaseRepository repository,
    TravelVisaCaseGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<RecordTravelVisaDecisionCommand, ApiResponse<TravelVisaDecisionResult>>
{
    public async Task<ApiResponse<TravelVisaDecisionResult>> Handle(RecordTravelVisaDecisionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.CaseId, tenantId, scope, cancellationToken);

        if (!VisaCaseStateMachine.AcceptsDecisions(state.Status))
            return ApiResponse<TravelVisaDecisionResult>.FailResponse($"Decisions can be recorded only after submission (case is {state.Status}).");

        var d = request.Decision;
        var approved = d.Decision == "Approved";
        var decision = d with
        {
            VisaNumber = approved && !string.IsNullOrWhiteSpace(d.VisaNumber) ? d.VisaNumber.Trim().ToUpperInvariant() : null,
            VisaIssueDate = approved ? d.VisaIssueDate?.Date : null,
            VisaValidFrom = approved ? d.VisaValidFrom?.Date : null,
            VisaExpiryDate = approved ? d.VisaExpiryDate?.Date : null,
            RejectionReason = d.Decision == "Rejected" ? d.RejectionReason!.Trim() : null,
            Notes = string.IsNullOrWhiteSpace(d.Notes) ? null : d.Notes.Trim()
        };

        var result = await repository.RecordDecisionAsync(tenantId, request.CaseId, request.ApplicantId, state.Status, decision,
            currentUser.UserId, DateTime.UtcNow, cancellationToken);
        if (!result.Applied)
            throw new ConflictException("The applicant was not found or the case changed. Reload and try again.");

        return ApiResponse<TravelVisaDecisionResult>.SuccessResponse(result,
            result.NewCaseStatus is null ? "Decision recorded." : $"Decision recorded. Case is now {result.NewCaseStatus}.");
    }
}
