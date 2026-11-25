using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;

public record RemoveTravelVisaApplicantCommand(int CaseId, int ApplicantId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "RemoveApplicant";
    public string AuditEntityName => "TravelVisaCase";
    public int? AuditEntityId => CaseId;
}

public class RemoveTravelVisaApplicantCommandValidator : AbstractValidator<RemoveTravelVisaApplicantCommand>
{
    public RemoveTravelVisaApplicantCommandValidator()
    {
        RuleFor(x => x.CaseId).GreaterThan(0);
        RuleFor(x => x.ApplicantId).GreaterThan(0);
    }
}

public class RemoveTravelVisaApplicantCommandHandler(
    ITravelVisaCaseRepository repository,
    TravelVisaCaseGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<RemoveTravelVisaApplicantCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RemoveTravelVisaApplicantCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetVisibleAsync(request.CaseId, tenantId, scope, cancellationToken);

        if (!VisaCaseStateMachine.IsPreSubmission(state.Status))
            return ApiResponse<bool>.FailResponse($"Applicants cannot be changed once a case is {state.Status}. Record a Withdrawn decision instead.");

        if (!await repository.RemoveApplicantAsync(tenantId, request.CaseId, request.ApplicantId, state.Status, currentUser.UserId, cancellationToken))
            throw new NotFoundException("TravelVisaApplicant", request.ApplicantId);
        return ApiResponse<bool>.SuccessResponse(true, "Applicant removed.");
    }
}
