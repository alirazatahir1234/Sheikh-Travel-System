using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

/// <summary>Creates (Id null) or updates a document type (passport, photo, bank statement…).</summary>
public record SaveTravelDocumentTypeCommand(int? Id, SaveTravelDocumentTypeDto DocumentType)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => Id.HasValue ? "Update" : "Create";
    public string AuditEntityName => "TravelDocumentType";
    public int? AuditEntityId => Id;
}

public class SaveTravelDocumentTypeCommandValidator : AbstractValidator<SaveTravelDocumentTypeCommand>
{
    public SaveTravelDocumentTypeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).When(x => x.Id.HasValue);
        RuleFor(x => x.DocumentType).NotNull();
        RuleFor(x => x.DocumentType.Code).NotEmpty().MaximumLength(40)
            .Matches("^[A-Z0-9_]+$").WithMessage("Code may contain only A-Z, 0-9 and underscore.");
        RuleFor(x => x.DocumentType.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DocumentType.Description).MaximumLength(500);
    }
}

public class SaveTravelDocumentTypeCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext, ICurrentUserService currentUser)
    : IRequestHandler<SaveTravelDocumentTypeCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.DocumentType with { Code = request.DocumentType.Code.Trim(), Name = request.DocumentType.Name.Trim() };

        if (await repository.DocumentTypeCodeExistsAsync(tenantId, dto.Code, request.Id, cancellationToken))
            return ApiResponse<int>.FailResponse("A document type with this code already exists.");

        if (request.Id is int id)
        {
            if (!await repository.UpdateDocumentTypeAsync(id, tenantId, dto, cancellationToken))
                throw new NotFoundException("TravelDocumentType", id);
            return ApiResponse<int>.SuccessResponse(id, "Document type updated.");
        }

        var newId = await repository.CreateDocumentTypeAsync(tenantId, dto, currentUser.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(newId, "Document type created.");
    }
}
