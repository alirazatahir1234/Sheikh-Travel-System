using System.Linq.Expressions;
using FluentValidation;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Customers.Commands;

/// <summary>Field rules shared by create and update travel customer validators.</summary>
internal static class TravelCustomerRules
{
    public static void Apply<T>(
        AbstractValidator<T> v,
        Expression<Func<T, string>> kind,
        Expression<Func<T, string>> fullName,
        Expression<Func<T, string>> phone,
        Expression<Func<T, string?>> email,
        Expression<Func<T, string?>> address,
        Expression<Func<T, string?>> city,
        Expression<Func<T, string?>> cnic,
        Expression<Func<T, string?>> contactPerson,
        Expression<Func<T, string?>> taxNumber,
        Expression<Func<T, string?>> alternatePhone,
        Expression<Func<T, string?>> notes)
    {
        v.RuleFor(kind).Must(k => TravelValues.IsOneOf(k, TravelValues.CustomerKinds))
            .WithMessage("Customer kind must be Individual or Corporate.");
        v.RuleFor(fullName).NotEmpty().MaximumLength(100);
        v.RuleFor(phone).NotEmpty().MaximumLength(30).Matches(@"^\+?[0-9 \-()]{7,30}$")
            .WithMessage("Enter a valid phone number.");
        v.RuleFor(email).MaximumLength(200).Matches(@"^$|^[^@\s]+@[^@\s]+\.[^@\s]+$")
            .WithMessage("Enter a valid email address.");
        v.RuleFor(address).MaximumLength(500);
        v.RuleFor(city).MaximumLength(100);
        v.RuleFor(cnic).MaximumLength(20);
        v.RuleFor(contactPerson).MaximumLength(150);
        v.RuleFor(taxNumber).MaximumLength(50);
        v.RuleFor(alternatePhone).MaximumLength(30);
        v.RuleFor(notes).MaximumLength(2000);
    }
}
