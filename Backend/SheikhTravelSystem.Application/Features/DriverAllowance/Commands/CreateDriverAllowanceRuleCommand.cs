using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.DriverAllowance.Commands;

public record CreateDriverAllowanceRuleCommand(CreateDriverAllowanceRuleDto Rule)
    : IRequest<ApiResponse<int>>;

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

public class CreateDriverAllowanceRuleCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateDriverAllowanceRuleCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(
        CreateDriverAllowanceRuleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Rule;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO DriverAllowanceRules
                    (Name, CalculationType, Value, Priority, MinDistanceKm, MaxDistanceKm,
                     VehicleFuelType, RouteFilter, IsActive, Notes, CreatedAt, CreatedBy, IsDeleted)
                  VALUES
                    (@Name, @CalculationType, @Value, @Priority, @MinDistanceKm, @MaxDistanceKm,
                     @VehicleFuelType, @RouteFilter, 1, @Notes, @CreatedAt, @CreatedBy, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.Name,
                    CalculationType = (int)dto.CalculationType,
                    dto.Value,
                    dto.Priority,
                    dto.MinDistanceKm,
                    dto.MaxDistanceKm,
                    VehicleFuelType = dto.VehicleFuelType.HasValue ? (int?)dto.VehicleFuelType : null,
                    dto.RouteFilter,
                    dto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "api"
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Driver allowance rule created successfully.");
    }
}
