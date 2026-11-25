using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Customers.Commands;

public record UpdateTravelCustomerCommand(int CustomerId, UpdateTravelCustomerDto Customer)
    : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "TravelCustomer";
    public int? AuditEntityId => CustomerId;
}

public class UpdateTravelCustomerCommandValidator : AbstractValidator<UpdateTravelCustomerCommand>
{
    public UpdateTravelCustomerCommandValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.Customer).NotNull();
        TravelCustomerRules.Apply(this, x => x.Customer.CustomerKind, x => x.Customer.FullName, x => x.Customer.Phone,
            x => x.Customer.Email, x => x.Customer.Address, x => x.Customer.City, x => x.Customer.Cnic,
            x => x.Customer.ContactPerson, x => x.Customer.TaxNumber, x => x.Customer.AlternatePhone, x => x.Customer.Notes);
    }
}

public class UpdateTravelCustomerCommandHandler(
    ITravelCustomerRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext)
    : IRequestHandler<UpdateTravelCustomerCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateTravelCustomerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var dto = request.Customer with { FullName = request.Customer.FullName.Trim() };

        _ = await repository.GetByIdAsync(request.CustomerId, tenantId, scope, cancellationToken)
            ?? throw new NotFoundException("TravelCustomer", request.CustomerId);

        if (!scope.Allows(dto.BranchId))
            throw new ForbiddenException("Branch is outside your data scope.");

        var refError = await repository.ValidateReferencesAsync(
            tenantId, dto.AssignedAgentUserId, dto.BranchId, dto.LeadSourceId, cancellationToken);
        if (refError is not null)
            return ApiResponse<bool>.FailResponse(refError);

        // null or a masked echo keeps the stored CNIC; "" clears it.
        string? cnic;
        if (dto.Cnic is null || TravelPii.IsMasked(dto.Cnic))
            cnic = await repository.GetCnicAsync(request.CustomerId, tenantId, cancellationToken);
        else
            cnic = string.IsNullOrWhiteSpace(dto.Cnic) ? null : dto.Cnic.Trim();

        if (cnic is not null)
        {
            var dup = await repository.FindCustomerIdByCnicAsync(tenantId, cnic, request.CustomerId, cancellationToken);
            if (dup.HasValue)
                return ApiResponse<bool>.FailResponse($"Another customer (#{dup.Value}) already has this CNIC.");
        }

        if (!await repository.UpdateAsync(request.CustomerId, tenantId, dto, cnic, cancellationToken))
            throw new NotFoundException("TravelCustomer", request.CustomerId);

        return ApiResponse<bool>.SuccessResponse(true, "Travel customer updated.");
    }
}
