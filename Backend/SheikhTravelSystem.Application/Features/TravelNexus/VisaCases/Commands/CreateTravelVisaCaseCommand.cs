using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.VisaCases.Commands;

public record CreateTravelVisaCaseCommand(CreateTravelVisaCaseDto VisaCase) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "TravelVisaCase";
    public int? AuditEntityId => null;
}

public class CreateTravelVisaCaseCommandValidator : AbstractValidator<CreateTravelVisaCaseCommand>
{
    public CreateTravelVisaCaseCommandValidator()
    {
        RuleFor(x => x.VisaCase).NotNull();
        RuleFor(x => x.VisaCase.CustomerId).GreaterThan(0);
        RuleFor(x => x.VisaCase.VisaTypeId).GreaterThan(0);
        RuleFor(x => x.VisaCase.EnquiryId).GreaterThan(0).When(x => x.VisaCase.EnquiryId.HasValue);
        TravelVisaCaseRules.ApplyCommon(this, x => x.VisaCase.Priority, x => x.VisaCase.QuotedServiceFee, x => x.VisaCase.QuotedEmbassyFee,
            x => x.VisaCase.QuotedServiceFee.HasValue || x.VisaCase.QuotedEmbassyFee.HasValue || !string.IsNullOrEmpty(x.VisaCase.CurrencyCode),
            x => x.VisaCase.CurrencyCode, x => x.VisaCase.Notes);
    }
}

public class CreateTravelVisaCaseCommandHandler(
    ITravelVisaCaseRepository repository,
    ITravelCustomerRepository customers,
    ITravelEnquiryRepository enquiries,
    ITravelConfigRepository config,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTravelVisaCaseCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateTravelVisaCaseCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var dto = request.VisaCase;

        var customer = await customers.GetRefAsync(dto.CustomerId, tenantId, cancellationToken);
        if (customer is null || !scope.Allows(customer.BranchId))
            throw new NotFoundException("TravelCustomer", dto.CustomerId);

        var visaType = await config.GetVisaTypeByIdAsync(dto.VisaTypeId, tenantId, cancellationToken);
        if (visaType is null || !visaType.IsActive)
            return ApiResponse<int>.FailResponse("Visa type not found or inactive.");

        if (dto.EnquiryId is int enquiryId)
        {
            var enquiry = await enquiries.GetByIdAsync(enquiryId, tenantId, scope, cancellationToken)
                ?? throw new NotFoundException("TravelEnquiry", enquiryId);
            if (enquiry.CustomerId is int linked && linked != dto.CustomerId)
                return ApiResponse<int>.FailResponse("The enquiry belongs to a different customer.");
        }

        var refError = await customers.ValidateReferencesAsync(tenantId, dto.AssignedAgentUserId, null, null, cancellationToken);
        if (refError is not null) return ApiResponse<int>.FailResponse(refError);

        var clean = dto with
        {
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? null : dto.CurrencyCode,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            IntendedTravelDate = dto.IntendedTravelDate?.Date,
            ExpectedDecisionDate = dto.ExpectedDecisionDate?.Date
        };
        var id = await repository.CreateAsync(tenantId, clean, visaType.CountryCode, customer.BranchId, currentUser.UserId,
            DateTime.UtcNow, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Visa case created.");
    }
}
