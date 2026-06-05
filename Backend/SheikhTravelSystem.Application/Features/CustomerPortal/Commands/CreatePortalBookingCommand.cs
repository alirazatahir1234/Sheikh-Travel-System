using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
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

public class CreatePortalBookingCommandHandler(IDbConnectionFactory dbFactory, ISender mediator)
    : IRequestHandler<CreatePortalBookingCommand, ApiResponse<PortalBookingCreatedDto>>
{
    public async Task<ApiResponse<PortalBookingCreatedDto>> Handle(CreatePortalBookingCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        using (var connection = dbFactory.CreateConnection())
        {
            var seating = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT SeatingCapacity FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                    new { Id = r.VehicleId },
                    cancellationToken: cancellationToken));

            if (seating is null or <= 0)
                return ApiResponse<PortalBookingCreatedDto>.FailResponse("Selected vehicle was not found.");

            if (r.PassengerCount > seating.Value)
                return ApiResponse<PortalBookingCreatedDto>.FailResponse(
                    $"Passenger count cannot exceed vehicle capacity ({seating.Value}).");
        }

        ApiResponse<PriceBreakdown> quote;
        if (r.PickupLat.HasValue && r.PickupLng.HasValue && r.DropLat.HasValue && r.DropLng.HasValue)
        {
            quote = await PortalDynamicPricingHelper.CalculatePointToPointQuoteAsync(
                mediator,
                dbFactory,
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
            quote = await PortalPricingHelper.CalculateQuoteAsync(
                mediator, dbFactory, r.RouteId.Value, r.VehicleId, r.IsRoundTrip, cancellationToken);
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
                using var promoConn = dbFactory.CreateConnection();
                promoId = await promoConn.ExecuteScalarAsync<int?>(
                    new CommandDefinition(
                        "SELECT Id FROM PromoCodes WHERE Code = @Code AND IsDeleted = 0",
                        new { Code = r.PromoCode.Trim().ToUpperInvariant() },
                        cancellationToken: cancellationToken));
            }
        }

        var finalTotal = breakdown.TotalAmount - discount;
        if (finalTotal <= 0)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse("Calculated total is invalid after discounts.");

        var effectiveRouteId = r.RouteId;
        if (effectiveRouteId is null or <= 0)
        {
            using var routeConn = dbFactory.CreateConnection();
            effectiveRouteId = await routeConn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT TOP 1 Id FROM Routes WHERE IsDeleted = 0 AND IsActive = 1 ORDER BY Id",
                    cancellationToken: cancellationToken));
        }

        var (customerOk, customerId, customerError) = await TryResolveCustomerIdAsync(dbFactory, mediator, r, cancellationToken);
        if (!customerOk)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(customerError ?? "Could not save your contact details.");

        var vehicleLabel = await GetVehicleLabelAsync(dbFactory, r.VehicleId, cancellationToken);
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
        var bookingNumber = await GetBookingNumberAsync(dbFactory, bookingId, cancellationToken);

        await mediator.Send(new AssignVehicleCommand(bookingId, r.VehicleId), cancellationToken);
        await ApplyPortalBookingExtrasAsync(dbFactory, bookingId, r, discount, promoId, cancellationToken);

        if (r.SeatLabels?.Count > 0)
        {
            using var seatConn = dbFactory.CreateConnection();
            var windowStart = r.PickupTime.AddHours(-3);
            var windowEnd = r.PickupTime.AddHours(3);
            foreach (var seat in r.SeatLabels.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var taken = await seatConn.ExecuteScalarAsync<bool>(new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM BookingSeats bs
                        INNER JOIN Bookings b ON b.Id = bs.BookingId
                        WHERE b.VehicleId = @VehicleId AND bs.SeatLabel = @SeatLabel
                          AND b.IsDeleted = 0 AND b.Status <> @Cancelled
                          AND b.PickupTime BETWEEN @Start AND @End) THEN 1 ELSE 0 END",
                    new
                    {
                        r.VehicleId,
                        SeatLabel = seat,
                        Cancelled = (int)BookingStatus.Cancelled,
                        Start = windowStart,
                        End = windowEnd
                    },
                    cancellationToken: cancellationToken));

                if (taken)
                    return ApiResponse<PortalBookingCreatedDto>.FailResponse($"Seat {seat} is already booked for this vehicle and time.");

                await seatConn.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO BookingSeats (BookingId, SeatLabel) VALUES (@BookingId, @SeatLabel)",
                        new { BookingId = bookingId, SeatLabel = seat },
                        cancellationToken: cancellationToken));
            }
        }

        await PortalCustomerWriter.WriteCustomerNotificationAsync(
            dbFactory,
            customerId,
            "Booking confirmed",
            $"Your booking {bookingNumber} has been received.",
            "BookingConfirmed",
            bookingId,
            cancellationToken);

        await PortalCustomerWriter.AddLoyaltyPointsAsync(dbFactory, customerId, (int)Math.Floor(finalTotal / 100), cancellationToken);

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

    private static async Task ApplyPortalBookingExtrasAsync(
        IDbConnectionFactory dbFactory,
        int bookingId,
        CreatePortalBookingRequest r,
        decimal discount,
        int? promoId,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE Bookings SET
                    PreferredPaymentMethod = @PreferredPaymentMethod,
                    PickupAddress = @PickupAddress,
                    DropoffAddress = @DropoffAddress,
                    PickupLat = @PickupLat,
                    PickupLng = @PickupLng,
                    DropLat = @DropLat,
                    DropLng = @DropLng,
                    QuotedDistanceKm = @QuotedDistanceKm,
                    QuotedDurationMinutes = @QuotedDurationMinutes,
                    AdultCount = @AdultCount,
                    ChildCount = @ChildCount,
                    LuggageCount = @LuggageCount,
                    PromoCodeId = @PromoCodeId,
                    DiscountAmount = @DiscountAmount,
                    UpdatedAt = SYSUTCDATETIME()
                  WHERE Id = @Id",
                new
                {
                    Id = bookingId,
                    r.PreferredPaymentMethod,
                    r.PickupAddress,
                    r.DropoffAddress,
                    r.PickupLat,
                    r.PickupLng,
                    r.DropLat,
                    r.DropLng,
                    r.QuotedDistanceKm,
                    r.QuotedDurationMinutes,
                    AdultCount = r.AdultCount ?? r.PassengerCount,
                    r.ChildCount,
                    r.LuggageCount,
                    PromoCodeId = promoId,
                    DiscountAmount = discount
                },
                cancellationToken: cancellationToken));
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

    private static async Task<(bool Ok, int CustomerId, string? Error)> TryResolveCustomerIdAsync(
        IDbConnectionFactory dbFactory,
        ISender mediator,
        CreatePortalBookingRequest r,
        CancellationToken cancellationToken)
    {
        var phone = PortalPhoneHelper.Normalize(r.Phone);
        var existingIds = await PortalBookingAccess.ResolvePortalCustomerIdsAsync(
            dbFactory, r.Phone, null, cancellationToken);
        if (existingIds.Count > 0)
            return (true, existingIds[0], null);

        var created = await mediator.Send(
            new CreateCustomerCommand(new CreateCustomerDto(r.FullName.Trim(), phone, r.Email?.Trim(), null, null)),
            cancellationToken);

        if (!created.Success)
            return (false, 0, created.Message);

        return (true, created.Data, null);
    }

    private static async Task<string?> GetVehicleLabelAsync(IDbConnectionFactory dbFactory, int vehicleId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT Name + N' (' + RegistrationNumber + N')' FROM Vehicles WHERE Id = @Id AND IsDeleted = 0",
                new { Id = vehicleId },
                cancellationToken: cancellationToken));
    }

    private static string? BuildNotes(string? userNotes, int vehicleId, string? vehicleLabel)
    {
        var portalLine = $"[Customer portal] Preferred vehicle #{vehicleId}" +
                         (string.IsNullOrWhiteSpace(vehicleLabel) ? "" : $": {vehicleLabel}");
        if (string.IsNullOrWhiteSpace(userNotes))
            return portalLine;
        return portalLine + Environment.NewLine + userNotes.Trim();
    }

    private static async Task<string?> GetBookingNumberAsync(IDbConnectionFactory dbFactory, int bookingId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT BookingNumber FROM Bookings WHERE Id = @Id AND IsDeleted = 0",
                new { Id = bookingId },
                cancellationToken: cancellationToken));
    }
}
