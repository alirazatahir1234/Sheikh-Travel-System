using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;

public record DeleteTravelTravelerCommand(int Id) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelTraveler";
    public int? AuditEntityId => Id;
}

public class DeleteTravelTravelerCommandValidator : AbstractValidator<DeleteTravelTravelerCommand>
{
    public DeleteTravelTravelerCommandValidator() => RuleFor(x => x.Id).GreaterThan(0);
}

public class DeleteTravelTravelerCommandHandler(
    ITravelTravelerRepository repository,
    TravelTravelerGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelTravelerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelTravelerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await repository.GetIdentityAsync(request.Id, tenantId, cancellationToken)
            ?? throw new NotFoundException("TravelTraveler", request.Id);
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        await guard.EnsureCustomerVisibleAsync(existing.CustomerId, tenantId, scope, cancellationToken);

        if (!await repository.DeleteAsync(request.Id, tenantId, cancellationToken))
            throw new NotFoundException("TravelTraveler", request.Id);
        return ApiResponse<bool>.SuccessResponse(true, "Traveler removed.");
    }
}
