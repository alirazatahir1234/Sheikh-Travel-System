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

/// <summary>Adds an ad-hoc document requirement for one applicant (beyond the checklist).</summary>
public record RequestTravelDocumentCommand(int CaseId, RequestTravelDocumentDto Request) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "RequestDocument";
    public string AuditEntityName => "TravelVisaCase";
    public int? AuditEntityId => CaseId;
}

public class RequestTravelDocumentCommandValidator : AbstractValidator<RequestTravelDocumentCommand>
{
    public RequestTravelDocumentCommandValidator()
    {
        RuleFor(x => x.CaseId).GreaterThan(0);
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.ApplicantId).GreaterThan(0);
        RuleFor(x => x.Request.DocumentTypeId).GreaterThan(0);
        RuleFor(x => x.Request.Instructions).MaximumLength(500);
    }
}

public class RequestTravelDocumentCommandHandler(
    ITravelDocumentRepository repository,
    ITravelVisaCaseRepository cases,
    ITravelConfigRepository config,
    TravelDocumentGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<RequestTravelDocumentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(RequestTravelDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var state = await guard.GetCaseAsync(request.CaseId, tenantId, scope, cancellationToken);
        if (TravelDocumentGuard.EditBlocker(state) is { } blocker) return ApiResponse<int>.FailResponse(blocker);

        var dto = request.Request;
        var applicants = await cases.GetApplicantsAsync(state.Id, tenantId, cancellationToken);
        if (!applicants.Any(a => a.Id == dto.ApplicantId && a.Decision != "Withdrawn"))
            return ApiResponse<int>.FailResponse("The applicant is not on this case.");

        var docType = await config.GetDocumentTypeByIdAsync(dto.DocumentTypeId, tenantId, cancellationToken);
        if (docType is null || !docType.IsActive) return ApiResponse<int>.FailResponse("Document type not found or inactive.");
        if (await repository.RequirementExistsAsync(tenantId, dto.ApplicantId, dto.DocumentTypeId, cancellationToken))
            return ApiResponse<int>.FailResponse($"{docType.Name} is already required for this applicant.");

        var id = await repository.RequestAsync(tenantId, state.Id, state.Status,
                dto with { Instructions = string.IsNullOrWhiteSpace(dto.Instructions) ? null : dto.Instructions.Trim() },
                currentUser.UserId, cancellationToken)
            ?? throw new ConflictException(TravelDocumentGuard.ConflictMessage);
        return ApiResponse<int>.SuccessResponse(id, $"{docType.Name} requested.");
    }
}
