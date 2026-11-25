using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Documents.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Documents.Queries;

/// <summary>Requirements of a case grouped by applicant, with file metadata (never contents) and progress.</summary>
public record GetTravelCaseDocumentsQuery(int CaseId) : IRequest<ApiResponse<TravelCaseDocumentsDto>>;

public class GetTravelCaseDocumentsQueryHandler(
    ITravelDocumentRepository repository,
    ITravelVisaCaseRepository cases,
    ITravelChecklistRepository checklists,
    TravelDocumentGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<GetTravelCaseDocumentsQuery, ApiResponse<TravelCaseDocumentsDto>>
{
    public async Task<ApiResponse<TravelCaseDocumentsDto>> Handle(GetTravelCaseDocumentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetCaseAsync(request.CaseId, tenantId, scope, cancellationToken);

        var requirements = await repository.GetCaseRequirementsAsync(state.Id, tenantId, cancellationToken);
        var files = (await repository.GetCaseFilesAsync(state.Id, tenantId, cancellationToken)).ToLookup(f => f.ApplicantDocumentId);
        foreach (var r in requirements) r.Files = files[r.Id].OrderByDescending(f => f.UploadedAt).ToList();

        var active = (await cases.GetApplicantsAsync(state.Id, tenantId, cancellationToken))
            .Where(a => a.Decision != "Withdrawn").Select(a => a.Id).ToHashSet();
        var progress = TravelChecklistRules.Progress(requirements.Select(r => new TravelRequirementState(r.ApplicantId, r.IsMandatory, r.Status)), active);

        var checklist = state.ChecklistTemplateId is int templateId ? await checklists.GetByIdAsync(templateId, tenantId, cancellationToken) : null;

        return ApiResponse<TravelCaseDocumentsDto>.SuccessResponse(new TravelCaseDocumentsDto
        {
            VisaCaseId = state.Id,
            CaseStatus = state.Status,
            ChecklistTemplateId = state.ChecklistTemplateId,
            ChecklistName = checklist?.Name,
            ChecklistVersion = checklist?.Version,
            MandatoryTotal = progress.MandatoryTotal,
            MandatoryComplete = progress.MandatoryComplete,
            CanEdit = TravelDocumentGuard.EditBlocker(state) is null,
            Requirements = requirements
        });
    }
}
