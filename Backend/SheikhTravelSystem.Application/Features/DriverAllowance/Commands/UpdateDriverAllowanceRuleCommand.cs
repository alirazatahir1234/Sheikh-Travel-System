using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class UpdateDriverAllowanceRuleCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateDriverAllowanceRuleCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(
        UpdateDriverAllowanceRuleCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Rule;

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM DriverAllowanceRules WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("DriverAllowanceRule", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE DriverAllowanceRules
                    SET Name = @Name, CalculationType = @CalculationType, Value = @Value,
                        Priority = @Priority, MinDistanceKm = @MinDistanceKm,
                        MaxDistanceKm = @MaxDistanceKm, VehicleFuelType = @VehicleFuelType,
                        RouteFilter = @RouteFilter, IsActive = @IsActive, Notes = @Notes,
                        UpdatedAt = @UpdatedAt, UpdatedBy = @UpdatedBy
                  WHERE Id = @Id",
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
                    dto.IsActive,
                    dto.Notes,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "api",
                    request.Id
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Driver allowance rule updated successfully.");
    }
}
