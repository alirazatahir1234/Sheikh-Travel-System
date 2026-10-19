using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Bookings.Commands;

public record AssignDriverCommand(int BookingId, int DriverId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "AssignDriver";
    public string AuditEntityName => "Booking";
    public int? AuditEntityId => BookingId;
}

public class AssignDriverCommandHandler(
    IBookingRepository bookingRepository,
    ITenantContext tenantContext,
    INotificationDecisionEngine decisionEngine,
    IWhatsAppAutomationHooks automationHooks)
    : IRequestHandler<AssignDriverCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(AssignDriverCommand request, CancellationToken cancellationToken)
    {
        var result = await bookingRepository.AssignDriverAsync(request.BookingId, request.DriverId, cancellationToken);
        if (!result.Success)
            return ApiResponse<bool>.FailResponse(result.ErrorMessage!);

        var tenantId = tenantContext.GetRequiredTenantId();

        // Driver app inbox: notify linked user only (ops already received booking_created).
        if (result.AssignedDriverUserId is int userId && userId > 0)
        {
            var bookingNumber = result.BookingNumber ?? $"#{request.BookingId}";
            await decisionEngine.DispatchIfAllowedAsync(new NotificationDecisionRequest(
                "booking_created",
                $"New Assignment: {bookingNumber}",
                $"You have been assigned to booking {bookingNumber}.",
                NotificationType.BookingCreated,
                ReferenceId: request.BookingId,
                TenantId: tenantId,
                TargetUserId: userId,
                SuggestedPriority: 2,
                RequestedChannels:
                [
                    NotificationChannels.InApp, NotificationChannels.Browser, NotificationChannels.Push
                ]), cancellationToken);
        }

        try
        {
            await automationHooks.OnDriverAssignedAsync(
                tenantId, request.BookingId, tripId: null, request.DriverId, vehicleId: null, cancellationToken);
        }
        catch
        {
            // Non-fatal
        }

        return ApiResponse<bool>.SuccessResponse(true, "Driver assigned successfully.");
    }
}
