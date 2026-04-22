using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Commands;

public record UpdateRouteCommand(int Id, UpdateRouteDto Route) : IRequest<ApiResponse<bool>>;

public class UpdateRouteCommandValidator : AbstractValidator<UpdateRouteCommand>
{
    public UpdateRouteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Route.Name).MaximumLength(200);
        RuleFor(x => x.Route.Source).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Route.Destination).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Route.Distance).GreaterThan(0);
        RuleFor(x => x.Route.EstimatedMinutes)
            .GreaterThan(0)
            .When(x => x.Route.EstimatedMinutes.HasValue);
        RuleFor(x => x.Route.BasePrice).GreaterThanOrEqualTo(0);
    }
}

public class UpdateRouteCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<UpdateRouteCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateRouteCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Route;

        var exists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Routes WHERE Id = @Id AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { request.Id },
                cancellationToken: cancellationToken));

        if (!exists)
            throw new NotFoundException("Route", request.Id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Routes SET Name = @Name, Source = @Source, Destination = @Destination,
                  Distance = @Distance, EstimatedMinutes = @EstimatedMinutes, BasePrice = @BasePrice,
                  IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new
                {
                    dto.Name, dto.Source, dto.Destination, dto.Distance,
                    dto.EstimatedMinutes, dto.BasePrice, dto.IsActive,
                    UpdatedAt = DateTime.UtcNow, request.Id
                },
                cancellationToken: cancellationToken));

        return ApiResponse<bool>.SuccessResponse(true, "Route updated successfully.");
    }
}
