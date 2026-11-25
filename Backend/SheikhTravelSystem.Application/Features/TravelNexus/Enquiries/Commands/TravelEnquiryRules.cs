using FluentValidation;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Enquiries.Commands;

public class SaveTravelEnquiryDtoValidator : AbstractValidator<SaveTravelEnquiryDto>
{
    public SaveTravelEnquiryDtoValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0).When(x => x.CustomerId.HasValue);
        RuleFor(x => x.LeadName).NotEmpty().WithMessage("Lead name is required when no customer is selected.")
            .When(x => !x.CustomerId.HasValue);
        RuleFor(x => x.LeadPhone).NotEmpty().WithMessage("Lead phone is required when no customer is selected.")
            .When(x => !x.CustomerId.HasValue);
        RuleFor(x => x.LeadName).MaximumLength(100);
        RuleFor(x => x.LeadPhone).MaximumLength(30).Matches(@"^$|^\+?[0-9 \-()]{7,30}$").WithMessage("Enter a valid phone number.");
        RuleFor(x => x.LeadEmail).MaximumLength(200).Matches(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("Enter a valid email address.");
        RuleFor(x => x.ServiceType).Must(v => TravelValues.IsOneOf(v, TravelValues.EnquiryServiceTypes)).WithMessage("Unknown service type.");
        RuleFor(x => x.Priority).Must(v => TravelValues.IsOneOf(v, TravelValues.Priorities)).WithMessage("Priority must be Low, Normal or High.");
        RuleFor(x => x.DestinationCountryCode).Must(TravelValues.IsCountryCode)
            .When(x => !string.IsNullOrEmpty(x.DestinationCountryCode)).WithMessage("Destination must be an ISO country code.");
        RuleFor(x => x.VisaTypeId).GreaterThan(0).When(x => x.VisaTypeId.HasValue);
        RuleFor(x => x.TravelDateTo).GreaterThanOrEqualTo(x => x.TravelDateFrom)
            .When(x => x.TravelDateFrom.HasValue && x.TravelDateTo.HasValue).WithMessage("Return date must be on or after departure.");
        RuleFor(x => x.Adults).InclusiveBetween(0, 500);
        RuleFor(x => x.Children).InclusiveBetween(0, 500);
        RuleFor(x => x.Infants).InclusiveBetween(0, 500);
        RuleFor(x => x).Must(x => x.Adults + x.Children + x.Infants >= 1).WithName("Adults").WithMessage("At least one traveler is required.");
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).When(x => x.Budget.HasValue);
        RuleFor(x => x.CurrencyCode).Must(TravelValues.IsCurrencyCode)
            .When(x => x.Budget.HasValue || !string.IsNullOrEmpty(x.CurrencyCode)).WithMessage("A budget needs an ISO 4217 currency code.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

/// <summary>Cross-record checks for enquiries: visibility, references, country/visa-type consistency.</summary>
public sealed class TravelEnquiryGuard(
    ITravelEnquiryRepository enquiries,
    ITravelCustomerRepository customers,
    ITravelConfigRepository config)
{
    /// <summary>Returns the enquiry or throws NotFound (also for other tenants and out-of-scope branches).</summary>
    public async Task<TravelEnquiryDto> GetVisibleAsync(int id, int tenantId, TravelBranchScope scope, CancellationToken ct)
        => await enquiries.GetByIdAsync(id, tenantId, scope, ct) ?? throw new NotFoundException("TravelEnquiry", id);

    /// <summary>
    /// Validates references and returns the payload with derived values (branch inherited from the customer,
    /// destination taken from the visa type). Returns an error message instead when invalid.
    /// </summary>
    public async Task<(SaveTravelEnquiryDto? Dto, string? Error)> PrepareAsync(
        int tenantId, TravelBranchScope scope, SaveTravelEnquiryDto dto, CancellationToken ct)
    {
        var branchId = dto.BranchId;
        if (dto.CustomerId is int customerId)
        {
            var customer = await customers.GetRefAsync(customerId, tenantId, ct);
            if (customer is null || !scope.Allows(customer.BranchId))
                throw new NotFoundException("TravelCustomer", customerId);
            branchId ??= customer.BranchId;
        }

        if (!scope.Allows(branchId))
            throw new ForbiddenException("Branch is outside your data scope.");

        var refError = await customers.ValidateReferencesAsync(tenantId, dto.AssignedAgentUserId, branchId, dto.LeadSourceId, ct);
        if (refError is not null) return (null, refError);

        var country = string.IsNullOrWhiteSpace(dto.DestinationCountryCode) ? null : dto.DestinationCountryCode;
        if (country is not null && !await config.CountryExistsAsync(country, ct))
            return (null, "Unknown destination country.");

        if (dto.VisaTypeId is int visaTypeId)
        {
            var visaType = await config.GetVisaTypeByIdAsync(visaTypeId, tenantId, ct);
            if (visaType is null) return (null, "Visa type not found.");
            if (country is not null && visaType.CountryCode != country)
                return (null, "The visa type belongs to a different country.");
            country = visaType.CountryCode;
        }

        return (dto with
        {
            BranchId = branchId,
            DestinationCountryCode = country,
            LeadName = Clean(dto.LeadName),
            LeadPhone = Clean(dto.LeadPhone),
            LeadEmail = Clean(dto.LeadEmail),
            CurrencyCode = Clean(dto.CurrencyCode),
            Notes = Clean(dto.Notes)
        }, null);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
