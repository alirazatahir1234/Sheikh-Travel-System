using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Customers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Customers.Commands;

/// <summary>Creates a travel customer, or attaches a travel profile to an existing Nexus customer. Returns the CustomerId.</summary>
public record CreateTravelCustomerCommand(CreateTravelCustomerDto Customer)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TravelCustomer";
    public int? AuditEntityId => Customer.ExistingCustomerId;
}

public class CreateTravelCustomerCommandValidator : AbstractValidator<CreateTravelCustomerCommand>
{
    public CreateTravelCustomerCommandValidator()
    {
        RuleFor(x => x.Customer).NotNull();
        RuleFor(x => x.Customer.ExistingCustomerId).GreaterThan(0).When(x => x.Customer.ExistingCustomerId.HasValue);
        TravelCustomerRules.Apply(this, x => x.Customer.CustomerKind, x => x.Customer.FullName, x => x.Customer.Phone,
            x => x.Customer.Email, x => x.Customer.Address, x => x.Customer.City, x => x.Customer.Cnic,
            x => x.Customer.ContactPerson, x => x.Customer.TaxNumber, x => x.Customer.AlternatePhone, x => x.Customer.Notes);
        RuleFor(x => x.Customer.Cnic).Must(c => !TravelPii.IsMasked(c)).WithMessage("Enter the full CNIC.");
    }
}

public class CreateTravelCustomerCommandHandler(
    ITravelCustomerRepository repository,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTravelCustomerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelCustomerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var dto = request.Customer with
        {
            FullName = request.Customer.FullName.Trim(),
            Cnic = string.IsNullOrWhiteSpace(request.Customer.Cnic) ? null : request.Customer.Cnic.Trim()
        };

        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        if (!scope.Allows(dto.BranchId))
            throw new ForbiddenException("Branch is outside your data scope.");

        var refError = await repository.ValidateReferencesAsync(
            tenantId, dto.AssignedAgentUserId, dto.BranchId, dto.LeadSourceId, cancellationToken);
        if (refError is not null)
            return ApiResponse<int>.FailResponse(refError);

        if (dto.ExistingCustomerId is int existingId)
        {
            var state = await repository.GetExistingCustomerStateAsync(existingId, tenantId, cancellationToken);
            if (state == ExistingCustomerState.NotFound)
                throw new NotFoundException("Customer", existingId);
            if (state == ExistingCustomerState.AlreadyTravelCustomer)
                return ApiResponse<int>.FailResponse("This customer is already a travel customer.");
        }

        if (dto.Cnic is not null)
        {
            var dup = await repository.FindCustomerIdByCnicAsync(tenantId, dto.Cnic, dto.ExistingCustomerId, cancellationToken);
            if (dup.HasValue)
                return ApiResponse<int>.FailResponse(
                    $"A customer with this CNIC already exists (customer #{dup.Value}). Link the existing customer instead.");
        }

        var customerId = await repository.CreateAsync(tenantId, dto, currentUser.UserId, cancellationToken);
        return ApiResponse<int>.SuccessResponse(customerId, "Travel customer created.");
    }
}
