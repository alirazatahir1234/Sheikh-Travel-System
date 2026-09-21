using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.Customers.Commands;
using SheikhTravelSystem.Application.Features.Customers.DTOs;
using SheikhTravelSystem.Application.Features.Payments.Commands;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Application.Features.Pricing.DTOs;

namespace SheikhTravelSystem.Application.Features.CustomerPortal.Commands;

public record CreatePortalBookingCommand(CreatePortalBookingRequest Request)
    : IRequest<ApiResponse<PortalBookingCreatedDto>>;

public class CreatePortalBookingCommandValidator : AbstractValidator<CreatePortalBookingCommand>
{
    public CreatePortalBookingCommandValidator()
    {
        RuleFor(x => x.Request.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Request.Email));
        RuleFor(x => x.Request.VehicleId).GreaterThan(0);
        RuleFor(x => x.Request)
            .Must(r => r.RouteId is > 0 || (r.PickupLat.HasValue && r.PickupLng.HasValue && r.DropLat.HasValue && r.DropLng.HasValue))
            .WithMessage("Select a route or provide pickup and drop-off locations.");
        RuleFor(x => x.Request.PickupTime).GreaterThan(DateTime.UtcNow)
            .WithMessage("Pickup time must be in the future.");
        RuleFor(x => x.Request.PassengerCount).InclusiveBetween(1, 60);
        RuleFor(x => x.Request.Notes).MaximumLength(900);
        RuleFor(x => x.Request.PaymentPlan).IsInEnum();

        When(x => x.Request.PaymentPlan == PortalPaymentPlan.Partial, () =>
        {
            RuleFor(x => x.Request.InitialPaymentAmount).NotNull().GreaterThan(0);
        });

        When(x => x.Request.PaymentPlan != PortalPaymentPlan.Partial, () =>
        {
            RuleFor(x => x.Request.InitialPaymentAmount).Null();
        });
    }
}

public class CreatePortalBookingCommandHandler(
    ICustomerPortalRepository portalRepository,
    IPortalPricingService pricingService,
    ISender mediator)
    : IRequestHandler<CreatePortalBookingCommand, ApiResponse<PortalBookingCreatedDto>>
{
    public async Task<ApiResponse<PortalBookingCreatedDto>> Handle(CreatePortalBookingCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        var seating = await portalRepository.GetVehicleSeatingCapacityAsync(r.VehicleId, cancellationToken);
        if (seating is null or <= 0)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse("Selected vehicle was not found.");

        if (r.PassengerCount > seating.Value)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(
                $"Passenger count cannot exceed vehicle capacity ({seating.Value}).");

        ApiResponse<PriceBreakdown> quote;
        if (r.PickupLat.HasValue && r.PickupLng.HasValue && r.DropLat.HasValue && r.DropLng.HasValue)
        {
            quote = await pricingService.CalculatePointToPointQuoteAsync(
                mediator,
                r.VehicleId,
                r.PickupLat.Value,
                r.PickupLng.Value,
                r.DropLat.Value,
                r.DropLng.Value,
                r.IsRoundTrip,
                r.RouteId,
                cancellationToken);
        }
        else if (r.RouteId is > 0)
        {
            quote = await pricingService.CalculateQuoteAsync(
                mediator, r.RouteId.Value, r.VehicleId, r.IsRoundTrip, cancellationToken);
        }
        else
        {
            return ApiResponse<PortalBookingCreatedDto>.FailResponse("Route or pickup/drop locations are required.");
        }

        if (!quote.Success || quote.Data is null)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(quote.Message ?? "Could not calculate price.");

        var breakdown = quote.Data;
        var discount = 0m;
        int? promoId = null;
        if (!string.IsNullOrWhiteSpace(r.PromoCode))
        {
            var promoResult = await mediator.Send(
                new ValidatePortalPromoCommand(r.Phone, new PortalValidatePromoRequest(r.PromoCode, breakdown.TotalAmount)),
                cancellationToken);
            if (promoResult.Success && promoResult.Data is { Valid: true } p)
            {
                discount = p.DiscountAmount;
                promoId = await portalRepository.GetPromoCodeIdAsync(r.PromoCode.Trim().ToUpperInvariant(), cancellationToken);
            }
        }

        var finalTotal = breakdown.TotalAmount - discount;
        if (finalTotal <= 0)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse("Calculated total is invalid after discounts.");

        var effectiveRouteId = r.RouteId;
        if (effectiveRouteId is null or <= 0)
            effectiveRouteId = await portalRepository.GetFirstActiveRouteIdAsync(cancellationToken);

        var (customerOk, customerId, customerError) = await TryResolveCustomerIdAsync(mediator, r, cancellationToken);
        if (!customerOk)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(customerError ?? "Could not save your contact details.");

        var vehicleLabel = await portalRepository.GetVehicleLabelAsync(r.VehicleId, cancellationToken);
        var combinedNotes = BuildNotes(r.Notes, r.VehicleId, vehicleLabel);

        var bookingResult = await mediator.Send(
            new CreateBookingCommand(
                new CreateBookingDto(
                    customerId,
                    effectiveRouteId!.Value,
                    r.PickupTime,
                    r.PassengerCount,
                    finalTotal,
                    combinedNotes)),
            cancellationToken);

        if (!bookingResult.Success)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(bookingResult.Message ?? "Booking could not be created.");

        var bookingId = bookingResult.Data;
        var bookingNumber = await portalRepository.GetBookingNumberAsync(bookingId, cancellationToken);

        await mediator.Send(new AssignVehicleCommand(bookingId, r.VehicleId), cancellationToken);
        await portalRepository.ApplyPortalBookingExtrasAsync(
            new PortalBookingExtrasUpdate
            {
                BookingId = bookingId,
                PreferredPaymentMethod = r.PreferredPaymentMethod,
                PickupAddress = r.PickupAddress,
                DropoffAddress = r.DropoffAddress,
                PickupLat = r.PickupLat,
                PickupLng = r.PickupLng,
                DropLat = r.DropLat,
                DropLng = r.DropLng,
                QuotedDistanceKm = r.QuotedDistanceKm,
                QuotedDurationMinutes = r.QuotedDurationMinutes,
                AdultCount = r.AdultCount ?? r.PassengerCount,
                ChildCount = r.ChildCount,
                LuggageCount = r.LuggageCount,
                PromoCodeId = promoId,
                DiscountAmount = discount
            },
            cancellationToken);

        if (r.SeatLabels?.Count > 0)
        {
            var windowStart = r.PickupTime.AddHours(-3);
            var windowEnd = r.PickupTime.AddHours(3);
            foreach (var seat in r.SeatLabels.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var taken = await portalRepository.IsSeatTakenAsync(
                    r.VehicleId, seat, windowStart, windowEnd, cancellationToken);

                if (taken)
                    return ApiResponse<PortalBookingCreatedDto>.FailResponse($"Seat {seat} is already booked for this vehicle and time.");

                await portalRepository.InsertBookingSeatAsync(bookingId, seat, cancellationToken);
            }
        }

        await portalRepository.WriteCustomerNotificationAsync(
            customerId,
            "Booking confirmed",
            $"Your booking {bookingNumber} has been received.",
            "BookingConfirmed",
            bookingId,
            cancellationToken);

        await portalRepository.AddLoyaltyPointsAsync(customerId, (int)Math.Floor(finalTotal / 100), cancellationToken);

        var payState = await ApplyInitialPortalPaymentAsync(
            mediator,
            r.PaymentPlan,
            r.InitialPaymentAmount,
            bookingId,
            finalTotal,
            r.PreferredPaymentMethod,
            cancellationToken);

        if (payState is null)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(
                $"Booking {bookingNumber} was created, but the initial payment could not be recorded. Please contact support.");

        var payload = new PortalBookingCreatedDto(
            bookingId,
            bookingNumber ?? string.Empty,
            finalTotal,
            breakdown with { TotalAmount = finalTotal },
            payState.Value);

        return ApiResponse<PortalBookingCreatedDto>.SuccessResponse(payload, "Your booking request has been received.");
    }

    private async Task<(bool Ok, int CustomerId, string? Error)> TryResolveCustomerIdAsync(
        ISender mediator,
        CreatePortalBookingRequest r,
        CancellationToken cancellationToken)
    {
        var phone = PortalPhoneHelper.Normalize(r.Phone);
        var existingIds = await portalRepository.ResolvePortalCustomerIdsAsync(r.Phone, null, cancellationToken);
        if (existingIds.Count > 0)
            return (true, existingIds[0], null);

        var created = await mediator.Send(
            new CreateCustomerCommand(new CreateCustomerDto(r.FullName.Trim(), phone, r.Email?.Trim(), null, null)),
            cancellationToken);

        if (!created.Success)
            return (false, 0, created.Message);

        return (true, created.Data, null);
    }

    private static async Task<PortalPayState?> ApplyInitialPortalPaymentAsync(
        ISender mediator,
        PortalPaymentPlan plan,
        decimal? initialAmount,
        int bookingId,
        decimal total,
        string? preferredMethod,
        CancellationToken cancellationToken)
    {
        switch (plan)
        {
            case PortalPaymentPlan.Full:
                var full = await mediator.Send(
                    new CreatePaymentCommand(
                        new CreatePaymentDto(
                            bookingId,
                            total,
                            preferredMethod ?? "CustomerPortal",
                            null,
                            "Full payment (customer portal)",
                            null)),
                    cancellationToken);
                return full.Success ? PortalPayState.Paid : null;

            case PortalPaymentPlan.Partial:
                var amt = initialAmount!.Value;
                if (amt <= 0 || amt >= total)
                    return null;
                var part = await mediator.Send(
                    new CreatePaymentCommand(
                        new CreatePaymentDto(
                            bookingId,
                            amt,
                            preferredMethod ?? "CustomerPortal",
                            null,
                            "Partial payment (customer portal)",
                            null)),
                    cancellationToken);
                return part.Success ? PortalPayState.PartiallyPaid : null;

            case PortalPaymentPlan.PayLater:
                return PortalPayState.Unpaid;

            default:
                return null;
        }
    }

    private static string? BuildNotes(string? userNotes, int vehicleId, string? vehicleLabel)
    {
        var portalLine = $"[Customer portal] Preferred vehicle #{vehicleId}" +
                         (string.IsNullOrWhiteSpace(vehicleLabel) ? "" : $": {vehicleLabel}");
        if (string.IsNullOrWhiteSpace(userNotes))
            return portalLine;
        return portalLine + Environment.NewLine + userNotes.Trim();
    }
}
