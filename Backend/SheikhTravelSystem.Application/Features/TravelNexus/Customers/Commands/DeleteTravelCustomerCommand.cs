using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Customers.Commands;

/// <summary>Archives the travel profile. The shared Nexus customer record is kept.</summary>
public record DeleteTravelCustomerCommand(int CustomerId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Delete";
    public string AuditEntityName => "TravelCustomer";
    public int? AuditEntityId => CustomerId;
}

public class DeleteTravelCustomerCommandValidator : AbstractValidator<DeleteTravelCustomerCommand>
{
    public DeleteTravelCustomerCommandValidator() => RuleFor(x => x.CustomerId).GreaterThan(0);
}

public class DeleteTravelCustomerCommandHandler(
    ITravelCustomerRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<DeleteTravelCustomerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelCustomerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        _ = await repository.GetByIdAsync(request.CustomerId, tenantId, scope, cancellationToken)
            ?? throw new NotFoundException("TravelCustomer", request.CustomerId);

        if (!await repository.DeleteAsync(request.CustomerId, tenantId, cancellationToken))
            throw new NotFoundException("TravelCustomer", request.CustomerId);
        return ApiResponse<bool>.SuccessResponse(true, "Travel customer archived.");
    }
}
