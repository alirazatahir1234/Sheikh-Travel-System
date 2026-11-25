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

/// <summary>Verify an uploaded document. Documents with a minimum validity need an expiry date that satisfies it.</summary>
public record VerifyTravelDocumentCommand(int RequirementId, VerifyTravelDocumentDto Verification) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Verify";
    public string AuditEntityName => "TravelApplicantDocument";
    public int? AuditEntityId => RequirementId;
}

public class VerifyTravelDocumentCommandValidator : AbstractValidator<VerifyTravelDocumentCommand>
{
    public VerifyTravelDocumentCommandValidator()
    {
        RuleFor(x => x.RequirementId).GreaterThan(0);
        RuleFor(x => x.Verification).NotNull();
        RuleFor(x => x.Verification.Note).MaximumLength(500);
    }
}

public class VerifyTravelDocumentCommandHandler(
    ITravelDocumentRepository repository, TravelDocumentGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<VerifyTravelDocumentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(VerifyTravelDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var (requirement, state) = await guard.GetAsync(request.RequirementId, tenantId, scope, cancellationToken);
        if (TravelDocumentGuard.EditBlocker(state) is { } blocker) return ApiResponse<bool>.FailResponse(blocker);
        if (requirement.Status != "Uploaded") return ApiResponse<bool>.FailResponse($"Only uploaded documents can be verified (this one is {requirement.Status}).");

        var expiry = request.Verification.ExpiryDate?.Date;
        var validity = TravelChecklistRules.ValidityProblem(requirement.MinValidityDays, expiry, (state.IntendedTravelDate ?? DateTime.UtcNow).Date);
        if (validity is not null) return ApiResponse<bool>.FailResponse(validity);

        var note = string.IsNullOrWhiteSpace(request.Verification.Note) ? null : request.Verification.Note.Trim();
        if (!await repository.ReviewAsync(tenantId, state.Id, requirement.Id, state.Status, ["Uploaded"], "Verified", note, expiry,
                currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelDocumentGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, $"{requirement.DocumentTypeName} verified.");
    }
}

/// <summary>Reject an uploaded or verified document (reason required); a new upload is then expected.</summary>
public record RejectTravelDocumentCommand(int RequirementId, ReviewReasonDto Review) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Reject";
    public string AuditEntityName => "TravelApplicantDocument";
    public int? AuditEntityId => RequirementId;
}

public class RejectTravelDocumentCommandValidator : AbstractValidator<RejectTravelDocumentCommand>
{
    public RejectTravelDocumentCommandValidator()
    {
        RuleFor(x => x.RequirementId).GreaterThan(0);
        RuleFor(x => x.Review).NotNull();
        RuleFor(x => x.Review.Reason).NotEmpty().WithMessage("Give a reason.").MaximumLength(500);
    }
}

public class RejectTravelDocumentCommandHandler(
    ITravelDocumentRepository repository, TravelDocumentGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<RejectTravelDocumentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(RejectTravelDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var (requirement, state) = await guard.GetAsync(request.RequirementId, tenantId, scope, cancellationToken);
        if (TravelDocumentGuard.EditBlocker(state) is { } blocker) return ApiResponse<bool>.FailResponse(blocker);
        if (requirement.Status is not ("Uploaded" or "Verified"))
            return ApiResponse<bool>.FailResponse($"Only uploaded or verified documents can be rejected (this one is {requirement.Status}).");

        if (!await repository.ReviewAsync(tenantId, state.Id, requirement.Id, state.Status, ["Uploaded", "Verified"], "Rejected",
                request.Review.Reason.Trim(), null, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelDocumentGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, $"{requirement.DocumentTypeName} rejected.");
    }
}

/// <summary>Waive a requirement (reason required) — counts as complete for the verify gate and is recorded in history.</summary>
public record WaiveTravelDocumentCommand(int RequirementId, ReviewReasonDto Review) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Waive";
    public string AuditEntityName => "TravelApplicantDocument";
    public int? AuditEntityId => RequirementId;
}

public class WaiveTravelDocumentCommandValidator : AbstractValidator<WaiveTravelDocumentCommand>
{
    public WaiveTravelDocumentCommandValidator()
    {
        RuleFor(x => x.RequirementId).GreaterThan(0);
        RuleFor(x => x.Review).NotNull();
        RuleFor(x => x.Review.Reason).NotEmpty().WithMessage("Give a reason for the waiver.").MaximumLength(500);
    }
}

public class WaiveTravelDocumentCommandHandler(
    ITravelDocumentRepository repository, TravelDocumentGuard guard, ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<WaiveTravelDocumentCommand, ApiResponse<bool>>
{
    private static readonly string[] Waivable = ["Missing", "Uploaded", "Rejected"];

    public async Task<ApiResponse<bool>> Handle(WaiveTravelDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var (requirement, state) = await guard.GetAsync(request.RequirementId, tenantId, scope, cancellationToken);
        if (TravelDocumentGuard.EditBlocker(state) is { } blocker) return ApiResponse<bool>.FailResponse(blocker);
        if (!Waivable.Contains(requirement.Status))
            return ApiResponse<bool>.FailResponse($"A {requirement.Status} document cannot be waived.");

        if (!await repository.ReviewAsync(tenantId, state.Id, requirement.Id, state.Status, Waivable, "Waived",
                request.Review.Reason.Trim(), null, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelDocumentGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, $"{requirement.DocumentTypeName} waived.");
    }
}
