using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

/// <summary>Soft-deletes a VisaType of the current tenant.</summary>
public record DeleteTravelVisaTypeCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelVisaType";
    public int? AuditEntityId => Id;
}

public class DeleteTravelVisaTypeCommandValidator : AbstractValidator<DeleteTravelVisaTypeCommand>
{
    public DeleteTravelVisaTypeCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelVisaTypeCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelVisaTypeCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelVisaTypeCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteVisaTypeAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken))
            throw new NotFoundException("TravelVisaType", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Deleted.");
    }
}
