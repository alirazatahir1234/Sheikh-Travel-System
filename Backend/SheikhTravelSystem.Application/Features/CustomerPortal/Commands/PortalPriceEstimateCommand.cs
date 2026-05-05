using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Commands;

public record PortalPriceEstimateCommand(PortalPriceEstimateRequest Request)
    : IRequest<ApiResponse<PriceBreakdown>>;

public class PortalPriceEstimateCommandValidator : AbstractValidator<PortalPriceEstimateCommand>
{
    public PortalPriceEstimateCommandValidator()
    {
        RuleFor(x => x.Request.RouteId).GreaterThan(0);
        RuleFor(x => x.Request.VehicleId).GreaterThan(0);
    }
}

public class PortalPriceEstimateCommandHandler(IDbConnectionFactory dbFactory, ISender mediator)
    : IRequestHandler<PortalPriceEstimateCommand, ApiResponse<PriceBreakdown>>
{
    public Task<ApiResponse<PriceBreakdown>> Handle(PortalPriceEstimateCommand request, CancellationToken cancellationToken) =>
        PortalPricingHelper.CalculateQuoteAsync(
            mediator,
            dbFactory,
            request.Request.RouteId,
            request.Request.VehicleId,
            request.Request.IsRoundTrip,
            cancellationToken);
}
