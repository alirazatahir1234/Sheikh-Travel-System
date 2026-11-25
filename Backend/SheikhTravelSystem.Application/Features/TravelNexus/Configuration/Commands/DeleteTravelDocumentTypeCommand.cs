using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

/// <summary>Soft-deletes a DocumentType of the current tenant.</summary>
public record DeleteTravelDocumentTypeCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelDocumentType";
    public int? AuditEntityId => Id;
}

public class DeleteTravelDocumentTypeCommandValidator : AbstractValidator<DeleteTravelDocumentTypeCommand>
{
    public DeleteTravelDocumentTypeCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelDocumentTypeCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelDocumentTypeCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelDocumentTypeCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteDocumentTypeAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken))
            throw new NotFoundException("TravelDocumentType", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Deleted.");
    }
}
