using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Commands;

public record CreateDriverAllowanceRuleCommand(CreateDriverAllowanceRuleDto Rule) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "DriverAllowanceRule";
    public int? AuditEntityId => null;
}

public class CreateDriverAllowanceRuleCommandValidator
    : AbstractValidator<CreateDriverAllowanceRuleCommand>
{
    public CreateDriverAllowanceRuleCommandValidator()
    {
        RuleFor(x => x.Rule.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Rule.CalculationType).IsInEnum();
        RuleFor(x => x.Rule.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Rule.Priority).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Rule.Value)
            .LessThanOrEqualTo(100)
            .When(x => x.Rule.CalculationType == AllowanceCalculationType.ProfitPercent)
            .WithMessage("Profit percent must be between 0 and 100.");

        RuleFor(x => x.Rule.MaxDistanceKm)
            .GreaterThan(x => x.Rule.MinDistanceKm ?? 0)
            .When(x => x.Rule.MaxDistanceKm.HasValue && x.Rule.MinDistanceKm.HasValue)
            .WithMessage("MaxDistanceKm must be greater than MinDistanceKm.");
    }
}

public class CreateDriverAllowanceRuleCommandHandler(IDriverAllowanceRepository repository)
    : IRequestHandler<CreateDriverAllowanceRuleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        CreateDriverAllowanceRuleCommand request, CancellationToken cancellationToken)
    {
        var id = await repository.CreateAsync(request.Rule, cancellationToken);
        return ApiResponse<int>.SuccessResponse(id, "Driver allowance rule created successfully.");
    }
}
