using FluentValidation;
using SheikhTravelSystem.Application.Common;

namespace SheikhTravelSystem.Application.Features.Tenants;

public class ProvisionTenantCommandValidator : AbstractValidator<ProvisionTenantCommand>
{
    private static readonly HashSet<string> ValidModuleCodes =
        TenantModuleCatalog.All.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public ProvisionTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(100)
            .Matches(@"^[a-z0-9-]+$").WithMessage("Slug must contain only lowercase letters, numbers, and hyphens.");

        RuleFor(x => x.Code).MaximumLength(50).When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.AdminFullName)
            .NotEmpty().WithMessage("Administrator full name is required.")
            .MaximumLength(200);

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("Administrator email is required.")
            .EmailAddress().WithMessage("Administrator email is invalid.")
            .MaximumLength(200);

        RuleFor(x => x.AdminPassword)
            .NotEmpty().WithMessage("Administrator password is required.")
            .MinimumLength(8).WithMessage("Administrator password must be at least 8 characters.");

        RuleFor(x => x.AdminMobile).MaximumLength(50).When(x => !string.IsNullOrWhiteSpace(x.AdminMobile));

        RuleFor(x => x.MaxUsers).GreaterThanOrEqualTo(0).When(x => x.MaxUsers.HasValue);
        RuleFor(x => x.MaxVehicles).GreaterThanOrEqualTo(0).When(x => x.MaxVehicles.HasValue);
        RuleFor(x => x.MaxDrivers).GreaterThanOrEqualTo(0).When(x => x.MaxDrivers.HasValue);
        RuleFor(x => x.MaxBranches).GreaterThanOrEqualTo(0).When(x => x.MaxBranches.HasValue);
        RuleFor(x => x.MaxGpsDevices).GreaterThanOrEqualTo(0).When(x => x.MaxGpsDevices.HasValue);

        RuleFor(x => x.ModuleCodes)
            .Must(codes => codes is null || codes.All(c => ValidModuleCodes.Contains(c)))
            .WithMessage("One or more module codes are invalid.")
            .When(x => x.ModuleCodes is { Count: > 0 });
    }
}
