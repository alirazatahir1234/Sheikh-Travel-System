using FluentValidation;

namespace SheikhTravelSystem.Application.Features.Platform;

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator() => DepartmentPayloadRules.Apply(RuleFor(x => x.Payload));
}

public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        DepartmentPayloadRules.Apply(RuleFor(x => x.Payload));
    }
}

internal static class DepartmentPayloadRules
{
    public static void Apply<T>(IRuleBuilder<T, DepartmentUpsertPayload> rule) where T : notnull
    {
        rule.NotNull();
        rule.ChildRules(payload =>
        {
            payload.RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Department name is required.")
                .MaximumLength(100);
        });
    }
}
