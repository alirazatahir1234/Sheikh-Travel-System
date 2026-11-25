using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Configuration.Commands;

/// <summary>Soft-deletes a LeadSource of the current tenant.</summary>
public record DeleteTravelLeadSourceCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelLeadSource";
    public int? AuditEntityId => Id;
}

public class DeleteTravelLeadSourceCommandValidator : AbstractValidator<DeleteTravelLeadSourceCommand>
{
    public DeleteTravelLeadSourceCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelLeadSourceCommandHandler(ITravelConfigRepository repository, ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelLeadSourceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelLeadSourceCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteLeadSourceAsync(request.Id, tenantContext.GetRequiredTenantId(), cancellationToken))
            throw new NotFoundException("TravelLeadSource", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Deleted.");
    }
}
