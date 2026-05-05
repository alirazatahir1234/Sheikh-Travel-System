using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
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
        RuleFor(x => x.Request.RouteId).GreaterThan(0);
        RuleFor(x => x.Request.VehicleId).GreaterThan(0);
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

        var quote = await PortalPricingHelper.CalculateQuoteAsync(
            mediator, dbFactory, r.RouteId, r.VehicleId, r.IsRoundTrip, cancellationToken);

        if (!quote.Success || quote.Data is null)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(quote.Message ?? "Could not calculate price.");

        var breakdown = quote.Data;
        if (breakdown.TotalAmount <= 0)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse("Calculated total is invalid. Please contact support.");

        var (customerOk, customerId, customerError) = await TryResolveCustomerIdAsync(dbFactory, mediator, r, cancellationToken);
        if (!customerOk)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(customerError ?? "Could not save your contact details.");

        var vehicleLabel = await GetVehicleLabelAsync(dbFactory, r.VehicleId, cancellationToken);
        var combinedNotes = BuildNotes(r.Notes, r.VehicleId, vehicleLabel);

        var bookingResult = await mediator.Send(
            new CreateBookingCommand(
                new CreateBookingDto(
                    customerId,
                    r.RouteId,
                    r.PickupTime,
                    r.PassengerCount,
                    breakdown.TotalAmount,
                    combinedNotes)),
            cancellationToken);

        if (!bookingResult.Success)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(bookingResult.Message ?? "Booking could not be created.");

        var bookingId = bookingResult.Data;
        var bookingNumber = await GetBookingNumberAsync(dbFactory, bookingId, cancellationToken);

        var payState = await ApplyInitialPortalPaymentAsync(
            mediator,
            r.PaymentPlan,
            r.InitialPaymentAmount,
            bookingId,
            breakdown.TotalAmount,
            cancellationToken);

        if (payState is null)
            return ApiResponse<PortalBookingCreatedDto>.FailResponse(
                $"Booking {bookingNumber} was created, but the initial payment could not be recorded. Please contact support.");

        var payload = new PortalBookingCreatedDto(
            bookingId,
            bookingNumber ?? string.Empty,
            breakdown.TotalAmount,
            breakdown,
            payState.Value);

        return ApiResponse<PortalBookingCreatedDto>.SuccessResponse(payload, "Your booking request has been received.");
    }

    private static async Task<PortalPayState?> ApplyInitialPortalPaymentAsync(
        ISender mediator,
        PortalPaymentPlan plan,
        decimal? initialAmount,
        int bookingId,
        decimal total,
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
                            "CustomerPortal",
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
                            "CustomerPortal",
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
        var phone = r.Phone.Trim();
        using var connection = dbFactory.CreateConnection();
        var existingId = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                @"SELECT TOP 1 Id FROM Customers WHERE Phone = @Phone AND IsDeleted = 0 ORDER BY Id",
                new { Phone = phone },
                cancellationToken: cancellationToken));

        if (existingId is > 0)
            return (true, existingId.Value, null);

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
