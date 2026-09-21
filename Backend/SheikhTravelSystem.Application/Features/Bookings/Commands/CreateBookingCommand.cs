using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

/// <summary>
/// Creates a new booking from the provided booking DTO.
/// </summary>
public record CreateBookingCommand(CreateBookingDto Booking) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "Create";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => null;
}

/// <summary>
/// Validates booking creation inputs.
/// </summary>
public class CreateBookingCommandValidator : AbstractValidator<CreateBookingCommand>
{
    public CreateBookingCommandValidator()
    {
        RuleFor(x => x.Booking.CustomerId).GreaterThan(0);
        RuleFor(x => x.Booking.RouteId).GreaterThan(0);
        RuleFor(x => x.Booking.PickupTime).GreaterThan(DateTime.UtcNow)
            .WithMessage("Pickup time must be in the future.");
        RuleFor(x => x.Booking.PassengerCount).GreaterThan(0);
        RuleFor(x => x.Booking.TotalAmount).GreaterThan(0);
    }
}

/// <summary>
/// Handles booking creation and prerequisite checks.
/// </summary>
public class CreateBookingCommandHandler(
    IBookingRepository bookingRepository,
    INotificationDecisionEngine decisionEngine,
    ILogger<CreateBookingCommandHandler> logger)
    : IRequestHandler<CreateBookingCommand, ApiResponse<int>>
{
    /// <summary>
    /// Validates related entities and inserts the booking record.
    /// </summary>
    public async Task<ApiResponse<int>> Handle(CreateBookingCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Booking;
        var created = await bookingRepository.CreateAsync(dto, cancellationToken);

        await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
            "booking_created",
            $"New Booking: {created.BookingNumber}",
            $"A new booking has been created for {created.RouteName}. Pickup: {dto.PickupTime:g}",
            NotificationType.BookingCreated,
            ReferenceId: created.Id,
            SuggestedPriority: 2,
            RequestedChannels:
            [
                NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Email
            ]), cancellationToken);

        logger.LogInformation(
            "Booking {BookingId} ({BookingNumber}) created for customer {CustomerId} on route {RouteId}",
            created.Id, created.BookingNumber, dto.CustomerId, dto.RouteId);
        return ApiResponse<int>.SuccessResponse(created.Id, "Booking created successfully.");
    }
}
