using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Platform;

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator() => BranchPayloadRules.Apply(RuleFor(x => x.Payload));
}

public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        BranchPayloadRules.Apply(RuleFor(x => x.Payload));
    }
}

public class CreateBranchForTenantCommandValidator : AbstractValidator<CreateBranchForTenantCommand>
{
    public CreateBranchForTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        BranchPayloadRules.Apply(RuleFor(x => x.Payload));
    }
}

public class UpdateBranchForTenantCommandValidator : AbstractValidator<UpdateBranchForTenantCommand>
{
    public UpdateBranchForTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).GreaterThan(0);
        RuleFor(x => x.BranchId).GreaterThan(0);
        BranchPayloadRules.Apply(RuleFor(x => x.Payload));
    }
}

internal static class BranchPayloadRules
{
    private static readonly Regex BranchNamePattern =
        new(@"^[A-Za-z0-9]+(?:[\s'&\-][A-Za-z0-9]+)*$", RegexOptions.Compiled);

    public static void Apply<T>(IRuleBuilder<T, BranchUpsertPayload> rule) where T : notnull
    {
        rule.NotNull();
        rule.ChildRules(payload =>
        {
            payload.RuleFor(x => x.BranchCode)
                .NotEmpty().WithMessage("Branch code is required.")
                .MaximumLength(50);

            payload.RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Branch name is required.")
                .MaximumLength(200)
                .Must(BeValidBranchName)
                .WithMessage("Branch name cannot be numeric only.");

            payload.RuleFor(x => x.BranchType).MaximumLength(50);
            payload.RuleFor(x => x.Phone)
                .MaximumLength(50)
                .Must(HaveAtMostFifteenDigits)
                .WithMessage("Phone number cannot exceed 15 digits.")
                .When(x => !string.IsNullOrWhiteSpace(x.Phone));
            payload.RuleFor(x => x.Email)
                .MaximumLength(200)
                .EmailAddress()
                .When(x => !string.IsNullOrWhiteSpace(x.Email));
            payload.RuleFor(x => x.Address).MaximumLength(500);
            payload.RuleFor(x => x.City).MaximumLength(100);
            payload.RuleFor(x => x.Country).MaximumLength(100);
            payload.RuleFor(x => x.TimeZone).MaximumLength(100);
            payload.RuleFor(x => x.CurrencyCode).MaximumLength(10);

            payload.RuleFor(x => x.Status)
                .Must(s => Enum.IsDefined(typeof(BranchStatus), s))
                .WithMessage("Invalid branch status.");
        });
    }

    private static bool BeValidBranchName(string? name)
    {
        var v = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(v)) return true;
        if (v.All(char.IsDigit) || !v.Any(char.IsLetter)) return false;
        return BranchNamePattern.IsMatch(v);
    }

    private static bool HaveAtMostFifteenDigits(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true;
        var digits = phone.Count(char.IsDigit);
        return digits <= 15;
    }
}
