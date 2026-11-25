using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;

public record SubmitTravelVisaCaseCommand(int Id, SubmitTravelVisaCaseDto Submission) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Submit";
    public string AuditEntityName => "TravelVisaCase";
    public int? AuditEntityId => Id;
}

public class SubmitTravelVisaCaseCommandValidator : AbstractValidator<SubmitTravelVisaCaseCommand>
{
    public SubmitTravelVisaCaseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Submission).NotNull();
        RuleFor(x => x.Submission.SubmittedAt).NotEmpty().LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Submission date cannot be in the future.");
        RuleFor(x => x.Submission.SubmissionReference).MaximumLength(100);
        RuleFor(x => x.Submission.ExpectedDecisionDate).GreaterThanOrEqualTo(x => x.Submission.SubmittedAt.Date)
            .When(x => x.Submission.ExpectedDecisionDate.HasValue).WithMessage("Expected decision must be on or after submission.");
        RuleFor(x => x.Submission.Note).MaximumLength(500);
    }
}

public class SubmitTravelVisaCaseCommandHandler(
    ITravelVisaCaseRepository repository,
    TravelVisaCaseGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<SubmitTravelVisaCaseCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SubmitTravelVisaCaseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);

        var target = VisaCaseStateMachine.Target(state.Status, VisaCaseStateMachine.Actions.Submit);
        if (target is null)
            return ApiResponse<bool>.FailResponse($"Only a case with verified documents can be submitted (this one is {state.Status}).");
        if (await guard.ActiveApplicantCountAsync(request.Id, tenantId, cancellationToken) == 0)
            return ApiResponse<bool>.FailResponse("A case needs at least one applicant to be submitted.");

        var s = request.Submission;
        var reference = string.IsNullOrWhiteSpace(s.SubmissionReference) ? null : s.SubmissionReference.Trim();
        var change = new TravelVisaCaseStatusChange(
            request.Id, state.Status, target, null,
            Note: string.IsNullOrWhiteSpace(s.Note) ? (reference is null ? null : $"Reference {reference}") : s.Note.Trim(),
            SubmittedAt: s.SubmittedAt,
            SubmissionReference: reference,
            ExpectedDecisionDate: s.ExpectedDecisionDate?.Date);

        if (!await repository.ChangeStatusAsync(tenantId, change, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelVisaCaseRules.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Case submitted.");
    }
}
