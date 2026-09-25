using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

public sealed class AutomationMessageComposer
{
    public sealed record ComposeResult(
        string TemplateName,
        string Language,
        IReadOnlyList<string> BodyParameters,
        string? BodyPreview,
        string? UrlButtonSuffix,
        IReadOnlyList<string>? QuickReplyPayloads);

    public ComposeResult? Compose(
        string eventType,
        AutomationBookingSnapshot? booking,
        AutomationTripSnapshot? trip,
        string language,
        string? trackingToken,
        string templateName)
    {
        var name = FirstName(booking?.CustomerName) ?? "Customer";
        var bookingNo = booking?.BookingNumber ?? trip?.TripNumber ?? "—";
        var pickupTime = FormatLocal(booking?.PickupTime ?? trip?.PickupAt);
        var pickupAddr = booking?.PickupAddress ?? trip?.PickupAddress ?? "—";
        var driver = trip?.DriverFirstName ?? booking?.DriverFirstName ?? "Driver";
        var vehicle = trip?.VehicleDescription ?? booking?.VehicleDescription ?? "Vehicle";
        var plate = trip?.PlateNumber ?? booking?.PlateNumber ?? "—";
        var lang = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
        if (lang.StartsWith("ur", StringComparison.Ordinal)) lang = "ur";
        else lang = "en";

        return eventType switch
        {
            WaAutomationEventType.BookingConfirmed => new ComposeResult(
                templateName, lang,
                [name, bookingNo, pickupTime, pickupAddr],
                null, null, null),

            WaAutomationEventType.BookingRescheduled => new ComposeResult(
                templateName, lang,
                [name, bookingNo, pickupTime, pickupAddr],
                null, null, null),

            WaAutomationEventType.BookingCancelled => new ComposeResult(
                templateName, lang,
                [name, bookingNo, pickupTime],
                null, null, null),

            WaAutomationEventType.PickupReminder => new ComposeResult(
                templateName, lang,
                [name, bookingNo, pickupTime, pickupAddr],
                null, null, null),

            WaAutomationEventType.DriverAssigned => new ComposeResult(
                templateName, lang,
                [name, bookingNo, driver, vehicle, plate, pickupTime],
                null, trackingToken, null),

            WaAutomationEventType.DriverEnRoute => new ComposeResult(
                templateName, lang,
                [driver, vehicle, plate, "15"],
                null, trackingToken, null),

            WaAutomationEventType.DriverArriving => new ComposeResult(
                templateName, lang,
                [driver, "5"],
                null, trackingToken, null),

            WaAutomationEventType.DriverArrived => new ComposeResult(
                templateName, lang,
                [driver, vehicle, plate],
                null, trackingToken, null),

            WaAutomationEventType.TripCompleted => ComposeTripCompleted(
                templateName, lang, name, trip),

            WaAutomationEventType.RentalReturnReminder => new ComposeResult(
                templateName, lang,
                [name, bookingNo, plate, pickupTime, pickupAddr],
                null, null, null),

            _ => null
        };
    }

    private static ComposeResult ComposeTripCompleted(
        string templateName, string lang, string name, AutomationTripSnapshot? trip)
    {
        var tripNo = trip?.TripNumber ?? "—";
        var km = trip?.DistanceKm is { } d ? d.ToString("0.#") : "—";
        var amount = trip?.TotalAmount is { } a
            ? $"{a:0.##} {trip.Currency ?? "PKR"}"
            : "—";
        var tripId = trip?.Id ?? 0;
        return new ComposeResult(
            templateName, lang,
            [name, tripNo, km, amount],
            null, null,
            [$"RATE:{tripId}:5", $"RATE:{tripId}:3", $"RATE:{tripId}:1"]);
    }

    public static string? ResolveRecipient(AutomationBookingSnapshot? booking, AutomationTripSnapshot? trip)
    {
        // Prefer customer phone; TripPassengers can be layered later.
        var phone = booking?.CustomerPhone;
        if (string.IsNullOrWhiteSpace(phone))
            return null;
        return WhatsAppPhone.ToE164(phone);
    }

    public static string ResolveLanguage(AutomationBookingSnapshot? booking, string? tenantDefault)
    {
        if (!string.IsNullOrWhiteSpace(booking?.PreferredLanguage))
            return booking.PreferredLanguage!.Trim().ToLowerInvariant()[..Math.Min(2, booking.PreferredLanguage.Trim().Length)];
        if (!string.IsNullOrWhiteSpace(tenantDefault))
            return tenantDefault.Trim().ToLowerInvariant()[..Math.Min(2, tenantDefault.Trim().Length)];
        return "en";
    }

    public static bool IsRelevant(
        string eventType,
        AutomationBookingSnapshot? booking,
        AutomationTripSnapshot? trip,
        DateTime utcNow)
    {
        if (eventType == WaAutomationEventType.BookingCancelled)
            return true;

        if (booking is not null && booking.Status == (int)BookingStatus.Cancelled)
            return false;

        if (eventType is WaAutomationEventType.DriverArriving or WaAutomationEventType.DriverArrived)
        {
            // Staleness: if due was set long ago we skip at processor using event DueAt.
            return trip is null || trip.Status == (int)TripStatus.Started || trip.Status == (int)TripStatus.AtPickup;
        }

        if (eventType == WaAutomationEventType.PickupReminder)
        {
            if (booking is null) return false;
            if (booking.PickupTime < utcNow) return false;
            if (trip is not null && trip.Status >= (int)TripStatus.Started) return false;
        }

        return true;
    }

    private static string FirstName(string? full)
    {
        if (string.IsNullOrWhiteSpace(full)) return "Customer";
        var part = full.Trim().Split(' ', 2)[0];
        return string.IsNullOrWhiteSpace(part) ? "Customer" : part;
    }

    private static string FormatLocal(DateTime? utcOrUnspecified)
    {
        if (utcOrUnspecified is null) return "—";
        var dt = utcOrUnspecified.Value;
        // Display in a friendly form; tenant TZ formatting can refine later.
        return dt.ToString("ddd d MMM, h:mm tt");
    }
}
