using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Documents.Commands;

/// <summary>Removes an ad-hoc requirement. Checklist requirements cannot be removed — waive them instead.</summary>
public record RemoveTravelDocumentRequirementCommand(int RequirementId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "RemoveRequirement";
    public string AuditEntityName => "TravelApplicantDocument";
    public int? AuditEntityId => RequirementId;
}

public class RemoveTravelDocumentRequirementCommandValidator : AbstractValidator<RemoveTravelDocumentRequirementCommand>
{
    public RemoveTravelDocumentRequirementCommandValidator() => RuleFor(x => x.RequirementId).GreaterThan(0);
}

public class RemoveTravelDocumentRequirementCommandHandler(
    ITravelDocumentRepository repository,
    TravelDocumentGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<RemoveTravelDocumentRequirementCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RemoveTravelDocumentRequirementCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var (requirement, state) = await guard.GetAsync(request.RequirementId, tenantId, scope, cancellationToken);
        if (TravelDocumentGuard.EditBlocker(state) is { } blocker) return ApiResponse<bool>.FailResponse(blocker);
        if (requirement.ChecklistItemId is not null)
            return ApiResponse<bool>.FailResponse("Checklist documents cannot be removed. Waive it with a reason instead.");

        if (!await repository.RemoveAdHocAsync(tenantId, state.Id, requirement.Id, state.Status, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelDocumentGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Requirement removed.");
    }
}
