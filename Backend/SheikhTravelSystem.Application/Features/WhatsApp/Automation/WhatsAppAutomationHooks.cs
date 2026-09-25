using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

/// <summary>Thin convenience wrappers used by booking/trip handlers.</summary>
public interface IWhatsAppAutomationHooks
{
    Task OnBookingStatusChangedAsync(int tenantId, int bookingId, BookingStatus newStatus, CancellationToken ct = default);

    Task OnBookingRescheduledAsync(int tenantId, int bookingId, DateTime pickupAt, CancellationToken ct = default);

    Task OnDriverAssignedAsync(
        int tenantId, int bookingId, int? tripId, int driverId, int? vehicleId, CancellationToken ct = default);

    Task OnTripStatusChangedAsync(
        int tenantId, int tripId, int? bookingId, TripStatus newStatus, CancellationToken ct = default);
}

public sealed class WhatsAppAutomationHooks(
    IWhatsAppAutomationTrigger trigger,
    IWhatsAppAutomationRepository repository) : IWhatsAppAutomationHooks
{
    public async Task OnBookingStatusChangedAsync(
        int tenantId, int bookingId, BookingStatus newStatus, CancellationToken ct = default)
    {
        if (newStatus == BookingStatus.Confirmed)
        {
            var id = await trigger.RaiseAsync(new AutomationEventRequest(
                tenantId,
                WaAutomationEventType.BookingConfirmed,
                bookingId,
                null,
                AutomationPolicy.DedupeKey(WaAutomationEventType.BookingConfirmed, bookingId)), ct: ct);

            // Schedule pickup reminder (DueAt = pickup - offset applied by processor/scheduler via separate event)
            var booking = await repository.GetBookingSnapshotAsync(tenantId, bookingId, ct);
            if (booking is not null)
            {
                var rule = await repository.GetRuleAsync(tenantId, WaAutomationEventType.PickupReminder, ct);
                var offset = rule?.OffsetMinutes ?? -60;
                var due = booking.PickupTime.ToUniversalTime().AddMinutes(offset);
                await trigger.RaiseAsync(new AutomationEventRequest(
                    tenantId,
                    WaAutomationEventType.PickupReminder,
                    bookingId,
                    null,
                    AutomationPolicy.DedupeKey(
                        WaAutomationEventType.PickupReminder, bookingId, booking.PickupTime.Ticks),
                    due), ct: ct);
            }

            _ = id;
            return;
        }

        if (newStatus == BookingStatus.Cancelled)
        {
            await repository.CancelPendingForBookingAsync(tenantId, bookingId, ct: ct);
            var trip = await repository.GetBookingSnapshotAsync(tenantId, bookingId, ct);
            // Revoke tracking for any trips linked later via cancel event
            await trigger.RaiseAsync(new AutomationEventRequest(
                tenantId,
                WaAutomationEventType.BookingCancelled,
                bookingId,
                null,
                AutomationPolicy.DedupeKey(WaAutomationEventType.BookingCancelled, bookingId)), ct: ct);
            _ = trip;
        }
    }

    public async Task OnBookingRescheduledAsync(
        int tenantId, int bookingId, DateTime pickupAt, CancellationToken ct = default)
    {
        await repository.CancelPendingForBookingAsync(
            tenantId, bookingId,
            [WaAutomationEventType.PickupReminder, WaAutomationEventType.RentalReturnReminder], ct);

        await trigger.RaiseAsync(new AutomationEventRequest(
            tenantId,
            WaAutomationEventType.BookingRescheduled,
            bookingId,
            null,
            AutomationPolicy.DedupeKey(
                WaAutomationEventType.BookingRescheduled, bookingId, pickupAt.Ticks)), ct: ct);

        var rule = await repository.GetRuleAsync(tenantId, WaAutomationEventType.PickupReminder, ct);
        var offset = rule?.OffsetMinutes ?? -60;
        await trigger.RaiseAsync(new AutomationEventRequest(
            tenantId,
            WaAutomationEventType.PickupReminder,
            bookingId,
            null,
            AutomationPolicy.DedupeKey(WaAutomationEventType.PickupReminder, bookingId, pickupAt.Ticks),
            pickupAt.ToUniversalTime().AddMinutes(offset)), ct: ct);
    }

    public async Task OnDriverAssignedAsync(
        int tenantId, int bookingId, int? tripId, int driverId, int? vehicleId, CancellationToken ct = default)
    {
        if (tripId is > 0)
            await repository.RevokeTrackingLinksForTripAsync(tripId.Value, ct);

        await trigger.RaiseAsync(new AutomationEventRequest(
            tenantId,
            WaAutomationEventType.DriverAssigned,
            bookingId,
            tripId,
            AutomationPolicy.DedupeKey(
                WaAutomationEventType.DriverAssigned, bookingId, $"{driverId}-{vehicleId ?? 0}")), ct: ct);
    }

    public async Task OnTripStatusChangedAsync(
        int tenantId, int tripId, int? bookingId, TripStatus newStatus, CancellationToken ct = default)
    {
        switch (newStatus)
        {
            case TripStatus.Started:
                await trigger.RaiseAsync(new AutomationEventRequest(
                    tenantId,
                    WaAutomationEventType.DriverEnRoute,
                    bookingId,
                    tripId,
                    AutomationPolicy.DedupeKey(WaAutomationEventType.DriverEnRoute, tripId)), ct: ct);
                break;

            case TripStatus.AtPickup:
                await trigger.RaiseAsync(new AutomationEventRequest(
                    tenantId,
                    WaAutomationEventType.DriverArrived,
                    bookingId,
                    tripId,
                    AutomationPolicy.DedupeKey(WaAutomationEventType.DriverArrived, tripId)), ct: ct);
                break;

            case TripStatus.Completed:
                await trigger.RaiseAsync(new AutomationEventRequest(
                    tenantId,
                    WaAutomationEventType.TripCompleted,
                    bookingId,
                    tripId,
                    AutomationPolicy.DedupeKey(WaAutomationEventType.TripCompleted, tripId),
                    DateTime.UtcNow.AddMinutes(2)), ct: ct);
                await repository.ExpireTrackingLinksForTripAsync(
                    tripId, DateTime.UtcNow.AddMinutes(30), ct);
                break;

            case TripStatus.Cancelled:
                await repository.CancelPendingForTripAsync(tenantId, tripId, ct);
                await repository.RevokeTrackingLinksForTripAsync(tripId, ct);
                break;
        }
    }
}
