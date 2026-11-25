using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;

/// <summary>Draft → Active; the previously active checklist for the same country/visa type is retired.</summary>
public record ActivateTravelChecklistCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Activate";
    public string AuditEntityName => "TravelChecklist";
    public int? AuditEntityId => Id;
}

public class ActivateTravelChecklistCommandValidator : AbstractValidator<ActivateTravelChecklistCommand>
{
    public ActivateTravelChecklistCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class ActivateTravelChecklistCommandHandler(ITravelChecklistRepository repository, TravelChecklistGuard guard, ITenantContext tenantContext)
    : IRequestHandler<ActivateTravelChecklistCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(ActivateTravelChecklistCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await guard.GetAsync(request.Id, tenantId, cancellationToken);
        if (existing.Status != "Draft") return ApiResponse<bool>.FailResponse($"A {existing.Status} checklist cannot be activated.");
        if (existing.Items.Count == 0) return ApiResponse<bool>.FailResponse("Add at least one document before activating.");

        if (!await repository.ActivateAsync(request.Id, tenantId, cancellationToken))
            throw new ConflictException("The checklist was changed by someone else. Reload and try again.");
        return ApiResponse<bool>.SuccessResponse(true, "Checklist activated. New visa cases will use it.");
    }
}
