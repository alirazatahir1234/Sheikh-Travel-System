using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Routes.DTOs;

namespace SheikhTravelSystem.Application.Features.Routes.Commands;

public record UpdateRouteCommand(int Id, UpdateRouteDto Route) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "Update";
    public string AuditEntityName => "Route";
    public int? AuditEntityId => Id;
}

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
        RuleFor(x => x.Route.OptimizeMode).MaximumLength(50);
        RuleFor(x => x.Route)
            .Must(r => !string.Equals(r.Source.Trim(), r.Destination.Trim(), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Origin and destination must be different.");
    }
}

public class UpdateRouteCommandHandler(IRouteRepository routeRepository)
    : IRequestHandler<UpdateRouteCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateRouteCommand request, CancellationToken cancellationToken)
    {
        await routeRepository.UpdateAsync(request.Id, request.Route, cancellationToken);
        return ApiResponse<bool>.SuccessResponse(true, "Route updated successfully.");
    }
}
