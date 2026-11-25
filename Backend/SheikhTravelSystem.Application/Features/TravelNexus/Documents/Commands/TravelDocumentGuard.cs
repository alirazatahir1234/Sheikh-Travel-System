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

/// <summary>Visibility and editability checks for applicant documents (case is the security root).</summary>
public sealed class TravelDocumentGuard(ITravelDocumentRepository documents, ITravelVisaCaseRepository cases)
{
    public const string ConflictMessage = "The document or case was changed by someone else. Reload and try again.";

    /// <summary>Requirement + its case, or NotFound when either is outside the caller's tenant / branch scope.</summary>
    public async Task<(TravelRequirementFacts Requirement, TravelVisaCaseState Case)> GetAsync(
        int requirementId, int tenantId, TravelBranchScope scope, CancellationToken ct)
    {
        var requirement = await documents.GetRequirementAsync(requirementId, tenantId, ct)
            ?? throw new NotFoundException("TravelApplicantDocument", requirementId);
        var state = await cases.GetStateAsync(requirement.VisaCaseId, tenantId, scope, ct)
            ?? throw new NotFoundException("TravelApplicantDocument", requirementId);
        return (requirement, state);
    }

    public async Task<TravelVisaCaseState> GetCaseAsync(int caseId, int tenantId, TravelBranchScope scope, CancellationToken ct)
        => await cases.GetStateAsync(caseId, tenantId, scope, ct) ?? throw new NotFoundException("TravelVisaCase", caseId);

    /// <summary>Documents change only while the case is collecting them.</summary>
    public static string? EditBlocker(TravelVisaCaseState state) => state.Status == "DocumentsPending"
        ? null
        : state.Status == "DocumentsVerified"
            ? "Documents are already verified. Use \"Reopen documents\" on the case to change them."
            : $"Documents can only be changed while the case is collecting documents (it is {state.Status}).";
}
