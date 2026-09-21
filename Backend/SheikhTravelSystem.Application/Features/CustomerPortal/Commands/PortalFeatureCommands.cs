using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Commands;

public record PortalPointToPointQuoteCommand(PortalPointToPointQuoteRequest Request)
    : IRequest<ApiResponse<PortalQuoteResultDto>>;

public class PortalPointToPointQuoteCommandValidator : AbstractValidator<PortalPointToPointQuoteCommand>
{
    public PortalPointToPointQuoteCommandValidator()
    {
        RuleFor(x => x.Request.VehicleId).GreaterThan(0);
    }
}

public class PortalPointToPointQuoteCommandHandler(
    IPortalPricingService pricingService,
    ICustomerPortalRepository portalRepository,
    ISender mediator)
    : IRequestHandler<PortalPointToPointQuoteCommand, ApiResponse<PortalQuoteResultDto>>
{
    public async Task<ApiResponse<PortalQuoteResultDto>> Handle(
        PortalPointToPointQuoteCommand request,
        CancellationToken cancellationToken)
    {
        var r = request.Request;
        var quote = await pricingService.CalculatePointToPointQuoteAsync(
            mediator,
            r.VehicleId,
            r.PickupLat,
            r.PickupLng,
            r.DropLat,
            r.DropLng,
            r.IsRoundTrip,
            r.RouteId,
            cancellationToken);

        if (!quote.Success || quote.Data is null)
            return ApiResponse<PortalQuoteResultDto>.FailResponse(quote.Message ?? "Could not calculate fare.");

        var distanceKm = PortalDynamicPricingHelper.HaversineDistanceKm(
            r.PickupLat, r.PickupLng, r.DropLat, r.DropLng);
        var duration = PortalDynamicPricingHelper.EstimateDurationMinutes(distanceKm);

        string? routeLabel = null;
        if (r.RouteId is > 0)
            routeLabel = await portalRepository.GetRouteLabelAsync(r.RouteId.Value, cancellationToken);

        return ApiResponse<PortalQuoteResultDto>.SuccessResponse(
            new PortalQuoteResultDto(quote.Data, distanceKm, duration, routeLabel),
            "Quote ready.");
    }
}

public record ValidatePortalPromoCommand(string Phone, PortalValidatePromoRequest Request)
    : IRequest<ApiResponse<PortalPromoResultDto>>;

public class ValidatePortalPromoCommandHandler(ICustomerPortalRepository portalRepository)
    : IRequestHandler<ValidatePortalPromoCommand, ApiResponse<PortalPromoResultDto>>
{
    public async Task<ApiResponse<PortalPromoResultDto>> Handle(
        ValidatePortalPromoCommand request,
        CancellationToken cancellationToken)
    {
        var code = request.Request.Code.Trim().ToUpperInvariant();
        var promo = await portalRepository.GetActivePromoAsync(code, cancellationToken);

        if (promo is null)
            return ApiResponse<PortalPromoResultDto>.SuccessResponse(
                new PortalPromoResultDto(false, code, 0, "Invalid or expired promo code."));

        var discount = promo.Value.Pct is > 0
            ? Math.Round(request.Request.QuoteTotal * promo.Value.Pct.Value / 100m, 2)
            : promo.Value.Fixed ?? 0;

        if (discount <= 0)
            return ApiResponse<PortalPromoResultDto>.SuccessResponse(
                new PortalPromoResultDto(false, code, 0, "Promo code has no discount value."));

        if (discount > request.Request.QuoteTotal)
            discount = request.Request.QuoteTotal;

        return ApiResponse<PortalPromoResultDto>.SuccessResponse(
            new PortalPromoResultDto(true, code, discount, $"Promo applied: PKR {discount:N2} off."),
            "Promo valid.");
    }
}

/// <summary>
/// Thin facade kept for Program.cs startup phone normalization.
/// </summary>
public static class PortalCustomerWriter
{
    public static Task NormalizeCustomerPhonesAsync(
        ICustomerPortalRepository portalRepository,
        CancellationToken cancellationToken = default)
        => portalRepository.NormalizeCustomerPhonesAsync(cancellationToken);
}
