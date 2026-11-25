using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;

/// <summary>Edits a Draft checklist's header (country, visa type, name, notes).</summary>
public record UpdateTravelChecklistCommand(int Id, SaveTravelChecklistTemplateDto Checklist) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TravelChecklist";
    public int? AuditEntityId => Id;
}

public class UpdateTravelChecklistCommandValidator : AbstractValidator<UpdateTravelChecklistCommand>
{
    public UpdateTravelChecklistCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Checklist).NotNull().SetValidator(new SaveTravelChecklistTemplateDtoValidator());
    }
}

public class UpdateTravelChecklistCommandHandler(ITravelChecklistRepository repository, TravelChecklistGuard guard, ITenantContext tenantContext)
    : IRequestHandler<UpdateTravelChecklistCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelChecklistCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await guard.GetAsync(request.Id, tenantId, cancellationToken);
        if (existing.Status != "Draft") return ApiResponse<bool>.FailResponse("Only draft checklists can be edited. Create a new version instead.");

        var dto = request.Checklist with { Name = request.Checklist.Name.Trim(), Notes = string.IsNullOrWhiteSpace(request.Checklist.Notes) ? null : request.Checklist.Notes.Trim() };
        var error = await guard.ValidateScopeAsync(tenantId, dto, cancellationToken);
        if (error is not null) return ApiResponse<bool>.FailResponse(error);

        if (!await repository.UpdateDraftAsync(request.Id, tenantId, dto, cancellationToken))
            throw new ConflictException("The checklist is no longer a draft. Reload and try again.");
        return ApiResponse<bool>.SuccessResponse(true, "Checklist updated.");
    }
}
