using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;

/// <summary>
/// Won: links an existing customer or creates one from the lead, optionally opens a Draft visa case —
/// all in one transaction.
/// </summary>
public record ConvertTravelEnquiryCommand(int Id, ConvertTravelEnquiryDto Conversion)
    : IRequest<ApiResponse<TravelEnquiryConversionResult>>, IAuditableCommand
{
    public string AuditAction => "Convert";
    public string AuditEntityName => "TravelEnquiry";
    public int? AuditEntityId => Id;
}

public class ConvertTravelEnquiryCommandValidator : AbstractValidator<ConvertTravelEnquiryCommand>
{
    public ConvertTravelEnquiryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Conversion).NotNull();
        RuleFor(x => x.Conversion.ExistingCustomerId).GreaterThan(0).When(x => x.Conversion.ExistingCustomerId.HasValue);
        RuleFor(x => x.Conversion.NewCustomerKind).Must(k => TravelValues.IsOneOf(k, TravelValues.CustomerKinds))
            .When(x => x.Conversion.NewCustomerKind is not null).WithMessage("Customer kind must be Individual or Corporate.");
        RuleFor(x => x.Conversion.VisaCountryCode).Must(TravelValues.IsCountryCode)
            .When(x => !string.IsNullOrEmpty(x.Conversion.VisaCountryCode));
        RuleFor(x => x.Conversion.VisaTypeId).GreaterThan(0).When(x => x.Conversion.VisaTypeId.HasValue);
    }
}

public class ConvertTravelEnquiryCommandHandler(
    ITravelEnquiryRepository repository,
    ITravelCustomerRepository customers,
    ITravelConfigRepository config,
    TravelEnquiryGuard guard,
    ITravelBranchScopeResolver scopeResolver,
    ITenantContext tenantContext,
    ICurrentUserService currentUser)
    : IRequestHandler<ConvertTravelEnquiryCommand, ApiResponse<TravelEnquiryConversionResult>>
{
    public async Task<ApiResponse<TravelEnquiryConversionResult>> Handle(ConvertTravelEnquiryCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var scope = await scopeResolver.ResolveAsync(tenantId, cancellationToken);
        var enquiry = await guard.GetVisibleAsync(request.Id, tenantId, scope, cancellationToken);
        var dto = request.Conversion;

        if (EnquiryStateMachine.Target(enquiry.Status, EnquiryStateMachine.Actions.Convert) is null)
            return Fail($"A {enquiry.Status} enquiry cannot be converted.");

        // Customer: explicit existing → enquiry's linked customer → new from lead.
        int? customerId = dto.ExistingCustomerId ?? enquiry.CustomerId;
        int? customerBranchId = enquiry.BranchId;
        TravelEnquiryNewCustomer? newCustomer = null;
        if (customerId is int cid)
        {
            var customer = await customers.GetRefAsync(cid, tenantId, cancellationToken);
            if (customer is null || !scope.Allows(customer.BranchId))
                throw new NotFoundException("TravelCustomer", cid);
            customerBranchId = customer.BranchId;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(enquiry.LeadName) || string.IsNullOrWhiteSpace(enquiry.LeadPhone))
                return Fail("The lead has no name or phone; choose an existing customer instead.");
            newCustomer = new TravelEnquiryNewCustomer(dto.NewCustomerKind ?? "Individual", enquiry.LeadName, enquiry.LeadPhone,
                enquiry.LeadEmail, enquiry.BranchId, enquiry.AssignedAgentUserId, enquiry.LeadSourceId);
        }

        TravelEnquiryNewVisaCase? visaCase = null;
        if (dto.CreateVisaCase)
        {
            var visaTypeId = dto.VisaTypeId ?? enquiry.VisaTypeId;
            if (visaTypeId is null) return Fail("Choose a visa type for the new visa case.");
            var visaType = await config.GetVisaTypeByIdAsync(visaTypeId.Value, tenantId, cancellationToken);
            if (visaType is null || !visaType.IsActive) return Fail("Visa type not found or inactive.");
            var country = string.IsNullOrEmpty(dto.VisaCountryCode) ? visaType.CountryCode : dto.VisaCountryCode;
            if (country != visaType.CountryCode) return Fail("The visa type belongs to a different country.");
            visaCase = new TravelEnquiryNewVisaCase(country, visaType.Id, dto.IntendedTravelDate?.Date ?? enquiry.TravelDateFrom,
                customerBranchId, enquiry.AssignedAgentUserId);
        }

        var plan = new TravelEnquiryConversionPlan(request.Id, enquiry.Status, customerId, newCustomer, visaCase);
        var result = await repository.ConvertAsync(tenantId, plan, currentUser.UserId, DateTime.UtcNow, cancellationToken)
            ?? throw new ConflictException("The enquiry was changed by someone else. Reload and try again.");

        return ApiResponse<TravelEnquiryConversionResult>.SuccessResponse(result,
            result.VisaCaseNumber is null ? "Enquiry converted." : $"Enquiry converted; visa case {result.VisaCaseNumber} opened.");
    }

    private static ApiResponse<TravelEnquiryConversionResult> Fail(string message)
        => ApiResponse<TravelEnquiryConversionResult>.FailResponse(message);
}
