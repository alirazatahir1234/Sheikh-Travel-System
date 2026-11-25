using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;

/// <summary>Deletes a Draft checklist. Active and retired versions are kept because cases reference them.</summary>
public record DeleteTravelChecklistCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelChecklist";
    public int? AuditEntityId => Id;
}

public class DeleteTravelChecklistCommandValidator : AbstractValidator<DeleteTravelChecklistCommand>
{
    public DeleteTravelChecklistCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelChecklistCommandHandler(ITravelChecklistRepository repository, TravelChecklistGuard guard, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelChecklistCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelChecklistCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await guard.GetAsync(request.Id, tenantId, cancellationToken);
        if (existing.Status != "Draft") return ApiResponse<bool>.FailResponse("Only drafts can be deleted.");
        if (!await repository.DeleteDraftAsync(request.Id, tenantId, cancellationToken))
            throw new ConflictException("The checklist is no longer a draft. Reload and try again.");
        return ApiResponse<bool>.SuccessResponse(true, "Draft deleted.");
    }
}
