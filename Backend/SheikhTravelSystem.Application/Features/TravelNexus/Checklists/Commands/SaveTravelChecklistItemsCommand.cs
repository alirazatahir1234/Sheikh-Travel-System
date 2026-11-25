using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Checklists.Commands;

/// <summary>Replaces the items of a Draft checklist.</summary>
public record SaveTravelChecklistItemsCommand(int Id, IReadOnlyList<SaveTravelChecklistItemDto> Items) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "UpdateItems";
    public string AuditEntityName => "TravelChecklist";
    public int? AuditEntityId => Id;
}

public class SaveTravelChecklistItemsCommandValidator : AbstractValidator<SaveTravelChecklistItemsCommand>
{
    public SaveTravelChecklistItemsCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Items).NotNull().Must(i => i.Count <= 100).WithMessage("A checklist can have at most 100 items.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.DocumentTypeId).GreaterThan(0);
            item.RuleFor(i => i.ApplicantCondition).Must(c => TravelValues.IsOneOf(c, TravelValues.ApplicantConditions))
                .WithMessage("Unknown applicant condition.");
            item.RuleFor(i => i.MinValidityDays).InclusiveBetween(1, 3650).When(i => i.MinValidityDays.HasValue);
            item.RuleFor(i => i.Instructions).MaximumLength(500);
        });
        RuleFor(x => x.Items).Must(items => items.Select(i => (i.DocumentTypeId, i.ApplicantCondition)).Distinct().Count() == items.Count)
            .WithMessage("Each document type can appear only once per applicant condition.");
    }
}

public class SaveTravelChecklistItemsCommandHandler(ITravelChecklistRepository repository, ITravelConfigRepository config,
    TravelChecklistGuard guard, ITenantContext tenantContext)
    : IRequestHandler<SaveTravelChecklistItemsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SaveTravelChecklistItemsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await guard.GetAsync(request.Id, tenantId, cancellationToken);
        if (existing.Status != "Draft") return ApiResponse<bool>.FailResponse("Only draft checklists can be edited. Create a new version instead.");

        var activeTypes = (await config.GetDocumentTypesAsync(tenantId, true, cancellationToken)).Select(d => d.Id).ToHashSet();
        if (request.Items.Any(i => !activeTypes.Contains(i.DocumentTypeId)))
            return ApiResponse<bool>.FailResponse("Every item needs an active document type of this company.");

        var items = request.Items.Select(i => i with { Instructions = string.IsNullOrWhiteSpace(i.Instructions) ? null : i.Instructions.Trim() }).ToList();
        if (!await repository.ReplaceItemsAsync(request.Id, tenantId, items, cancellationToken))
            throw new ConflictException("The checklist is no longer a draft. Reload and try again.");
        return ApiResponse<bool>.SuccessResponse(true, "Checklist items saved.");
    }
}
