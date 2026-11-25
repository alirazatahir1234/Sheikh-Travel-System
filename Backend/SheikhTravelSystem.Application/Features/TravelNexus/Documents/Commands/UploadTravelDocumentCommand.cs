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

/// <summary>Uploads a new file version for a requirement (stored privately; never exposed by URL).</summary>
public record UploadTravelDocumentCommand(int RequirementId, string FileName, byte[] Content) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Upload";
    public string AuditEntityName => "TravelApplicantDocument";
    public int? AuditEntityId => RequirementId;
}

public class UploadTravelDocumentCommandValidator : AbstractValidator<UploadTravelDocumentCommand>
{
    public UploadTravelDocumentCommandValidator()
    {
        RuleFor(x => x.RequirementId).GreaterThan(0);
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotNull();
    }
}

public class UploadTravelDocumentCommandHandler(
    ITravelDocumentRepository repository,
    ITravelDocumentStorage storage,
    TravelDocumentGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<UploadTravelDocumentCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(UploadTravelDocumentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var (requirement, state) = await guard.GetAsync(request.RequirementId, tenantId, scope, cancellationToken);
        if (TravelDocumentGuard.EditBlocker(state) is { } blocker) return ApiResponse<int>.FailResponse(blocker);

        var check = TravelDocumentFileValidator.Validate(request.FileName, request.Content);
        if (!check.IsValid) return ApiResponse<int>.FailResponse(check.Error!);
        if (await repository.FileHashExistsAsync(tenantId, requirement.Id, check.Sha256, cancellationToken))
            return ApiResponse<int>.FailResponse("This exact file is already uploaded for this document.");

        var key = await storage.SaveAsync(request.Content, check.ContentType, check.Extension, tenantId, state.Id, cancellationToken);
        var originalName = Path.GetFileName(request.FileName);
        var fileId = await repository.AddFileAsync(tenantId, state.Id, requirement.Id, state.Status,
            new TravelNewDocumentFile(key, originalName.Length > 200 ? originalName[^200..] : originalName, check.ContentType,
                request.Content.LongLength, check.Sha256),
            currentUser.UserId, cancellationToken);

        if (fileId is null)
        {
            await storage.DeleteAsync(key, cancellationToken);
            throw new ConflictException(TravelDocumentGuard.ConflictMessage);
        }
        return ApiResponse<int>.SuccessResponse(fileId.Value, $"{requirement.DocumentTypeName} uploaded.");
    }
}
