using FluentValidation;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Travelers.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Travelers.Commands;

/// <summary>Field validation shared by create and update traveler commands.</summary>
public class SaveTravelTravelerDtoValidator : AbstractValidator<SaveTravelTravelerDto>
{
    public SaveTravelTravelerDtoValidator()
    {
        RuleFor(x => x.Title).MaximumLength(10);
        RuleFor(x => x.GivenNames).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Surname).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Gender).Must(g => TravelValues.IsOneOf(g, TravelValues.Genders))
            .WithMessage("Gender must be Male or Female (as printed in the passport).");
        RuleFor(x => x.DateOfBirth).Must(d => d!.Value.Date <= DateTime.UtcNow.Date && d.Value.Year >= 1900)
            .When(x => x.DateOfBirth.HasValue).WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.PlaceOfBirth).MaximumLength(100);
        RuleFor(x => x.NationalityCode).Must(TravelValues.IsCountryCode)
            .When(x => !string.IsNullOrEmpty(x.NationalityCode)).WithMessage("Nationality must be an ISO country code.");
        RuleFor(x => x.PassportNumber).Matches("^[A-Za-z0-9]{5,20}$")
            .When(x => !string.IsNullOrEmpty(x.PassportNumber) && !TravelPii.IsMasked(x.PassportNumber))
            .WithMessage("Passport number must be 5–20 letters or digits.");
        RuleFor(x => x.PassportIssuingCountryCode).Must(TravelValues.IsCountryCode)
            .When(x => !string.IsNullOrEmpty(x.PassportNumber))
            .WithMessage("Issuing country is required with a passport number.");
        RuleFor(x => x.PassportIssueDate).Must(d => d!.Value.Date <= DateTime.UtcNow.Date)
            .When(x => x.PassportIssueDate.HasValue).WithMessage("Issue date cannot be in the future.");
        RuleFor(x => x.PassportExpiryDate).GreaterThan(x => x.PassportIssueDate)
            .When(x => x.PassportIssueDate.HasValue && x.PassportExpiryDate.HasValue)
            .WithMessage("Expiry date must be after the issue date.");
        RuleFor(x => x.Cnic).Matches(@"^\d{5}-?\d{7}-?\d$")
            .When(x => !string.IsNullOrEmpty(x.Cnic) && !TravelPii.IsMasked(x.Cnic))
            .WithMessage("CNIC must be 13 digits (e.g. 12345-1234567-1).");
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200).Matches(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$")
            .WithMessage("Enter a valid email address.");
        RuleFor(x => x.MahramTravelerId).GreaterThan(0).When(x => x.MahramTravelerId.HasValue);
        RuleFor(x => x.MahramRelation).Must(r => TravelValues.IsOneOf(r, TravelValues.MahramRelations))
            .When(x => x.MahramTravelerId.HasValue).WithMessage("Choose the mahram relation.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

/// <summary>Cross-record rules (tenant/branch scope, mahram, passport uniqueness, country references).</summary>
public sealed class TravelTravelerGuard(
    ITravelCustomerRepository customers,
    ITravelTravelerRepository travelers,
    ITravelConfigRepository config)
{
    /// <summary>Throws NotFound when the customer is not a travel customer visible to the caller.</summary>
    public async Task EnsureCustomerVisibleAsync(int customerId, int tenantId, TravelBranchScope scope, CancellationToken ct)
    {
        var customer = await customers.GetRefAsync(customerId, tenantId, ct);
        if (customer is null || !scope.Allows(customer.BranchId))
            throw new NotFoundException("TravelCustomer", customerId);
    }

    /// <summary>Returns a user-facing error, or null when the traveler can be saved.</summary>
    public async Task<string?> ValidateAsync(
        int tenantId, int customerId, int? travelerId, SaveTravelTravelerDto dto, CancellationToken ct)
    {
        foreach (var code in new[] { dto.NationalityCode, dto.PassportIssuingCountryCode })
        {
            if (!string.IsNullOrEmpty(code) && !await config.CountryExistsAsync(code, ct))
                return $"Unknown country '{code}'.";
        }

        if (dto.PassportNumber is not null && dto.PassportIssuingCountryCode is not null
            && await travelers.PassportExistsAsync(tenantId, dto.PassportIssuingCountryCode, dto.PassportNumber, travelerId, ct))
            return "Another traveler already has this passport number.";

        if (dto.MahramTravelerId is int mahramId)
        {
            if (travelerId == mahramId)
                return "A traveler cannot be their own mahram.";
            var mahram = await travelers.GetIdentityAsync(mahramId, tenantId, ct);
            if (mahram is null || mahram.CustomerId != customerId)
                return "Mahram must be another traveler of the same customer.";
            if (mahram.Gender != "Male")
                return "Mahram must be male.";
        }

        return null;
    }
}

public static class TravelTravelerNormalizer
{
    /// <summary>Trims, upper-cases codes and passport; resolves keep/clear for identity fields.</summary>
    public static SaveTravelTravelerDto Normalize(SaveTravelTravelerDto dto, TravelTravelerIdentity? existing)
    {
        static string? Upper(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();

        static string? Resolve(string? incoming, string? stored, Func<string, string> clean)
        {
            if (incoming is null || TravelPii.IsMasked(incoming)) return stored;
            return string.IsNullOrWhiteSpace(incoming) ? null : clean(incoming.Trim());
        }

        return dto with
        {
            Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),
            GivenNames = dto.GivenNames.Trim().ToUpperInvariant(),
            Surname = dto.Surname.Trim().ToUpperInvariant(),
            NationalityCode = Upper(dto.NationalityCode),
            PassportIssuingCountryCode = Upper(dto.PassportIssuingCountryCode),
            PassportNumber = Resolve(dto.PassportNumber, existing?.PassportNumber, s => s.ToUpperInvariant()),
            Cnic = Resolve(dto.Cnic, existing?.Cnic, s => s.Replace("-", "")),
            MahramRelation = dto.MahramTravelerId.HasValue ? dto.MahramRelation : null,
            PassportIssueDate = dto.PassportIssueDate?.Date,
            PassportExpiryDate = dto.PassportExpiryDate?.Date,
            DateOfBirth = dto.DateOfBirth?.Date,
        };
    }
}
