using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

/// <summary>Soft-deletes a LostReason of the current tenant.</summary>
public record DeleteTravelLostReasonCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelLostReason";
    public int? AuditEntityId => Id;
}

public class DeleteTravelLostReasonCommandValidator : AbstractValidator<DeleteTravelLostReasonCommand>
{
    public DeleteTravelLostReasonCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelLostReasonCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelLostReasonCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelLostReasonCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteLostReasonAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken))
            throw new NotFoundException("TravelLostReason", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Deleted.");
    }
}
