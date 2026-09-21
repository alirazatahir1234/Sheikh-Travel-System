using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Commands;

public record UpdateDriverAllowanceRuleCommand(int Id, UpdateDriverAllowanceRuleDto Rule) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "DriverAllowanceRule";
    public int? AuditEntityId => Id;
}

public class UpdateDriverAllowanceRuleCommandValidator
    : AbstractValidator<UpdateDriverAllowanceRuleCommand>
{
    public UpdateDriverAllowanceRuleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
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

public class UpdateDriverAllowanceRuleCommandHandler(IDriverAllowanceRepository repository)
    : IRequestHandler<UpdateDriverAllowanceRuleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateDriverAllowanceRuleCommand request, CancellationToken cancellationToken)
    {
        if (!await repository.ExistsAsync(request.Id, cancellationToken))
            throw new NotFoundException("DriverAllowanceRule", request.Id);

        await repository.UpdateAsync(request.Id, request.Rule, cancellationToken);

        return ApiResponse<bool>.SuccessResponse(true, "Driver allowance rule updated successfully.");
    }
}
