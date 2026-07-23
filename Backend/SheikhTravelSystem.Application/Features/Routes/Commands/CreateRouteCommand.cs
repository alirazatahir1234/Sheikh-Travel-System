using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Commands;

public record CreateRouteCommand(CreateRouteDto Route) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Route";
    public int? AuditEntityId => null;
}

public class CreateRouteCommandValidator : AbstractValidator<CreateRouteCommand>
{
    public CreateRouteCommandValidator()
    {
        RuleFor(x => x.Route.Name).MaximumLength(200);
        RuleFor(x => x.Route.Source).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Route.Destination).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Route.Distance).GreaterThan(0);
        RuleFor(x => x.Route.EstimatedMinutes)
            .GreaterThan(0)
            .When(x => x.Route.EstimatedMinutes.HasValue);
        RuleFor(x => x.Route.BasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Route.OptimizeMode).MaximumLength(50);
        RuleFor(x => x.Route)
            .Must(r => !string.Equals(r.Source.Trim(), r.Destination.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Origin and destination must be different.");
    }
}

public class CreateRouteCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<CreateRouteCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Route;

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO Routes (Name, Source, Destination, Distance, EstimatedMinutes, BasePrice, IsActive, CreatedAt, IsDeleted, WaypointsJson, OptimizeMode)
                  VALUES (@Name, @Source, @Destination, @Distance, @EstimatedMinutes, @BasePrice, 1, @CreatedAt, 0, @WaypointsJson, @OptimizeMode);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    dto.Name, dto.Source, dto.Destination, dto.Distance,
                    dto.EstimatedMinutes, dto.BasePrice,
                    dto.WaypointsJson, dto.OptimizeMode,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Route created successfully.");
    }
}
