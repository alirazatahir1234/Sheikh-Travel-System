using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.WhatsApp;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Bot;

public interface IWhatsAppSelfServiceBot
{
    Task<WhatsAppSelfServiceReply?> ProcessAsync(
        WhatsAppSelfServiceContext ctx,
        CancellationToken cancellationToken = default);
}

public sealed record WhatsAppSelfServiceContext(
    int TenantId,
    int ConversationId,
    string PhoneE164,
    string? ProfileName,
    string? CurrentBotState,
    bool IsBotEnabled,
    string? UserText,
    string? ButtonPayload,
    string? FlowIdempotencyKey = null,
    string? FlowPayloadJson = null);

public sealed class WhatsAppSelfServiceBot(
    IWhatsAppSelfServiceRepository repository,
    IMediator mediator,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppSelfServiceBot> logger) : IWhatsAppSelfServiceBot
{
    public async Task<WhatsAppSelfServiceReply?> ProcessAsync(
        WhatsAppSelfServiceContext ctx,
        CancellationToken cancellationToken = default)
    {
        if (!ctx.IsBotEnabled)
            return null;

        var text = (ctx.ButtonPayload ?? ctx.UserText)?.Trim() ?? "";
        var state = ctx.CurrentBotState?.Trim() ?? "";

        // Flow nfm_reply → complete booking (Phase 3.2)
        if (!string.IsNullOrWhiteSpace(ctx.FlowPayloadJson)
            && !string.IsNullOrWhiteSpace(ctx.FlowIdempotencyKey))
        {
            return await CompleteBookingFromFlowAsync(ctx, cancellationToken);
        }

        if (string.IsNullOrEmpty(state)
            || state.Equals(WhatsAppDemoBotStates.Idle, StringComparison.OrdinalIgnoreCase)
            || state.Equals(WhatsAppDemoBotStates.Completed, StringComparison.OrdinalIgnoreCase)
            || WhatsAppSelfServiceMenu.IsMenuEntryKeyword(text)
            || WhatsAppSelfServiceMenu.MatchMenuId(text) == WhatsAppSelfServiceMenu.MenuAgain)
        {
            if (string.Equals(text, "DEMO", StringComparison.OrdinalIgnoreCase))
                return null; // let demo bot handle

            // Idle + non-empty that isn't menu keyword: still show menu (self-service default)
            if (string.IsNullOrEmpty(state)
                || state.Equals(WhatsAppDemoBotStates.Idle, StringComparison.OrdinalIgnoreCase)
                || state.Equals(WhatsAppDemoBotStates.Completed, StringComparison.OrdinalIgnoreCase)
                || WhatsAppSelfServiceMenu.IsMenuEntryKeyword(text)
                || WhatsAppSelfServiceMenu.MatchMenuId(text) == WhatsAppSelfServiceMenu.MenuAgain)
            {
                return MainMenuReply();
            }
        }

        if (state.Equals(WhatsAppSelfServiceStates.MainMenu, StringComparison.OrdinalIgnoreCase)
            || state.Equals(WhatsAppSelfServiceStates.Invoices, StringComparison.OrdinalIgnoreCase))
        {
            var menuId = WhatsAppSelfServiceMenu.MatchMenuId(text);
            if (menuId is not null)
                return await HandleMenuSelectionAsync(ctx, menuId, cancellationToken);

            if (text.StartsWith(WhatsAppSelfServiceMenu.PayPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(text.AsSpan(WhatsAppSelfServiceMenu.PayPrefix.Length), out var payBookingId))
                return await HandlePayAsync(ctx, payBookingId, cancellationToken);

            if (text.StartsWith(WhatsAppSelfServiceMenu.ViewPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(text.AsSpan(WhatsAppSelfServiceMenu.ViewPrefix.Length), out var viewBookingId))
                return await HandleViewInvoiceAsync(ctx, viewBookingId, cancellationToken);

            if (state.Equals(WhatsAppSelfServiceStates.MainMenu, StringComparison.OrdinalIgnoreCase))
                return UnknownMenuReply();
        }

        return state switch
        {
            WhatsAppSelfServiceStates.AwaitTripRef => await HandleTripRefAsync(ctx, text, cancellationToken),
            WhatsAppSelfServiceStates.AwaitBookingPickup => await HandleBookingPickupAsync(ctx, text, cancellationToken),
            WhatsAppSelfServiceStates.AwaitBookingDestination => await HandleBookingFieldAsync(
                ctx, text, s => s.Destination = text, WhatsAppSelfServiceStates.AwaitBookingDate,
                "What date do you need the vehicle? (e.g. 25 Sep 2026)", cancellationToken),
            WhatsAppSelfServiceStates.AwaitBookingDate => await HandleBookingDateAsync(ctx, text, cancellationToken),
            WhatsAppSelfServiceStates.AwaitBookingTime => await HandleBookingTimeAsync(ctx, text, cancellationToken),
            WhatsAppSelfServiceStates.AwaitBookingVehicleType => await HandleBookingVehicleAsync(ctx, text, cancellationToken),
            WhatsAppSelfServiceStates.AwaitBookingPassengers => await HandleBookingPassengersAsync(ctx, text, cancellationToken),
            WhatsAppSelfServiceStates.AwaitBookingNotes => await HandleBookingNotesAsync(ctx, text, cancellationToken),
            _ => MainMenuReply()
        };
    }

    private static WhatsAppSelfServiceReply MainMenuReply()
        => new(
            WhatsAppSelfServiceStates.MainMenu,
            WhatsAppSelfServiceCopy.Welcome + "\n\n1. Book a Vehicle\n2. Track My Trip\n3. My Invoices\n4. Talk to an Agent",
            ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
            ListButtonLabel: "Menu",
            ClearSession: true);

    private static WhatsAppSelfServiceReply UnknownMenuReply()
        => new(
            WhatsAppSelfServiceStates.MainMenu,
            WhatsAppSelfServiceCopy.UnknownMenu,
            ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
            ListButtonLabel: "Menu");

    private async Task<WhatsAppSelfServiceReply> HandleMenuSelectionAsync(
        WhatsAppSelfServiceContext ctx, string menuId, CancellationToken ct)
    {
        return menuId switch
        {
            WhatsAppSelfServiceMenu.Book => new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitBookingPickup,
                WhatsAppSelfServiceCopy.AskPickup,
                Session: new WhatsAppBotSessionData()),

            WhatsAppSelfServiceMenu.Track => new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitTripRef,
                WhatsAppSelfServiceCopy.AskTripRef),

            WhatsAppSelfServiceMenu.Invoices => await BuildInvoicesReplyAsync(ctx, ct),

            WhatsAppSelfServiceMenu.Agent => new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.HumanHandoff,
                WhatsAppSelfServiceCopy.AgentHandoff,
                DisableBot: true),

            _ => UnknownMenuReply()
        };
    }

    private async Task<WhatsAppSelfServiceReply> HandleTripRefAsync(
        WhatsAppSelfServiceContext ctx, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
        {
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitTripRef,
                WhatsAppSelfServiceCopy.AskTripRef);
        }

        var trip = await repository.FindAuthorizedTripOrBookingAsync(ctx.TenantId, ctx.PhoneE164, text, ct);
        if (trip is null)
        {
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.MainMenu,
                WhatsAppSelfServiceCopy.TripNotFound,
                ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
                ListButtonLabel: "Menu");
        }

        var lines = new List<string>
        {
            "Trip summary",
            "",
            $"Reference: {trip.TripNumber ?? trip.BookingNumber ?? "—"}",
            $"Status: {trip.StatusLabel}",
            $"Driver: {trip.DriverFirstName ?? "—"}",
            $"Vehicle: {trip.VehicleDescription ?? "—"} — {trip.PlateNumber ?? "—"}"
        };
        if (!string.IsNullOrWhiteSpace(trip.TrackingUrl))
            lines.Add($"Live tracking: {trip.TrackingUrl}");

        return new WhatsAppSelfServiceReply(
            WhatsAppSelfServiceStates.MainMenu,
            string.Join('\n', lines),
            ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
            ListButtonLabel: "Menu",
            ReplyButtons:
            [
                new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.MenuAgain, "Main menu"),
                new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.Agent, "Talk to Agent")
            ]);
    }

    private async Task<WhatsAppSelfServiceReply> BuildInvoicesReplyAsync(
        WhatsAppSelfServiceContext ctx, CancellationToken ct)
    {
        var items = await repository.ListInvoicesForPhoneAsync(ctx.TenantId, ctx.PhoneE164, 5, ct);
        if (items.Count == 0)
        {
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.MainMenu,
                WhatsAppSelfServiceCopy.NoInvoices,
                ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
                ListButtonLabel: "Menu");
        }

        var lines = new List<string> { "Your invoices", "" };
        var buttons = new List<WhatsAppInteractiveRow>();
        foreach (var inv in items.Take(3))
        {
            var due = inv.TotalAmount - inv.PaidAmount;
            var status = due <= 0 ? "Paid" : "Unpaid";
            lines.Add($"{inv.BookingNumber}");
            lines.Add($"{inv.Currency} {inv.TotalAmount:0.##} · {status}");
            lines.Add("");
            if (due > 0 && buttons.Count < 3)
                buttons.Add(new WhatsAppInteractiveRow(
                    $"{WhatsAppSelfServiceMenu.PayPrefix}{inv.BookingId}",
                    TruncateBtn($"Pay {inv.BookingNumber}")));
        }

        if (buttons.Count == 0)
            buttons.Add(new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.MenuAgain, "Main menu"));

        return new WhatsAppSelfServiceReply(
            WhatsAppSelfServiceStates.Invoices,
            string.Join('\n', lines).TrimEnd(),
            ReplyButtons: buttons);
    }

    private async Task<WhatsAppSelfServiceReply> HandleViewInvoiceAsync(
        WhatsAppSelfServiceContext ctx, int bookingId, CancellationToken ct)
    {
        var inv = await repository.GetAuthorizedInvoiceAsync(ctx.TenantId, ctx.PhoneE164, bookingId, ct);
        if (inv is null)
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.MainMenu,
                WhatsAppSelfServiceCopy.NoInvoices,
                ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
                ListButtonLabel: "Menu");

        var due = inv.TotalAmount - inv.PaidAmount;
        var body =
            $"Invoice: {inv.BookingNumber}\nDate: {inv.PickupTime:dd MMM yyyy}\nAmount: {inv.Currency} {inv.TotalAmount:0.##}\nStatus: {(due <= 0 ? "Paid" : "Unpaid")}";
        var buttons = new List<WhatsAppInteractiveRow>
        {
            new(WhatsAppSelfServiceMenu.MenuAgain, "Main menu")
        };
        if (due > 0)
            buttons.Insert(0, new WhatsAppInteractiveRow($"{WhatsAppSelfServiceMenu.PayPrefix}{inv.BookingId}", "Pay Now"));

        return new WhatsAppSelfServiceReply(
            WhatsAppSelfServiceStates.Invoices,
            body,
            ReplyButtons: buttons);
    }

    private async Task<WhatsAppSelfServiceReply> HandlePayAsync(
        WhatsAppSelfServiceContext ctx, int bookingId, CancellationToken ct)
    {
        var inv = await repository.GetAuthorizedInvoiceAsync(ctx.TenantId, ctx.PhoneE164, bookingId, ct);
        if (inv is null)
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.MainMenu,
                WhatsAppSelfServiceCopy.NoInvoices,
                ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
                ListButtonLabel: "Menu");

        var due = inv.TotalAmount - inv.PaidAmount;
        if (due <= 0)
        {
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.Invoices,
                $"Invoice {inv.BookingNumber} is already paid.",
                ReplyButtons: [new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.MenuAgain, "Main menu")]);
        }

        try
        {
            var checkout = await mediator.Send(new CreatePortalPaymentCheckoutCommand(
                inv.BookingId, due, ctx.PhoneE164, null), ct);

            if (!checkout.Success || checkout.Data is null)
            {
                return new WhatsAppSelfServiceReply(
                    WhatsAppSelfServiceStates.Invoices,
                    string.IsNullOrWhiteSpace(checkout.Message)
                        ? WhatsAppSelfServiceCopy.PayComingSoon
                        : checkout.Message!,
                    ReplyButtons:
                    [
                        new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.Agent, "Talk to Agent"),
                        new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.MenuAgain, "Main menu")
                    ]);
            }

            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.Invoices,
                $"Invoice: {inv.BookingNumber}\nAmount: {inv.Currency} {due:0.##}\nStatus: Unpaid\n\nPay securely:\n{checkout.Data.CheckoutUrl}",
                ReplyButtons: [new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.MenuAgain, "Main menu")]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WhatsApp pay checkout failed for booking {BookingId}", bookingId);
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.Invoices,
                WhatsAppSelfServiceCopy.PayComingSoon,
                ReplyButtons:
                [
                    new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.Agent, "Talk to Agent"),
                    new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.MenuAgain, "Main menu")
                ]);
        }
    }

    private async Task<WhatsAppSelfServiceReply> HandleBookingPickupAsync(
        WhatsAppSelfServiceContext ctx, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
            return new WhatsAppSelfServiceReply(WhatsAppSelfServiceStates.AwaitBookingPickup, WhatsAppSelfServiceCopy.AskPickup);

        var session = await LoadSessionAsync(ctx, ct);
        session.Pickup = text.Trim();
        return new WhatsAppSelfServiceReply(
            WhatsAppSelfServiceStates.AwaitBookingDestination,
            "Where should we drop you off?",
            Session: session);
    }

    private async Task<WhatsAppSelfServiceReply> HandleBookingFieldAsync(
        WhatsAppSelfServiceContext ctx,
        string text,
        Action<WhatsAppBotSessionData> apply,
        string nextState,
        string nextPrompt,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
            return new WhatsAppSelfServiceReply(ctx.CurrentBotState ?? nextState, "Please send a valid reply.");

        var session = await LoadSessionAsync(ctx, ct);
        apply(session);
        return new WhatsAppSelfServiceReply(nextState, nextPrompt, Session: session);
    }

    private async Task<WhatsAppSelfServiceReply> HandleBookingDateAsync(
        WhatsAppSelfServiceContext ctx, string text, CancellationToken ct)
    {
        if (!TryParseDate(text, out _))
        {
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitBookingDate,
                "Please send a valid future date (e.g. 25 Sep 2026).");
        }

        var session = await LoadSessionAsync(ctx, ct);
        session.Date = text.Trim();
        return new WhatsAppSelfServiceReply(
            WhatsAppSelfServiceStates.AwaitBookingTime,
            "What pickup time? (e.g. 18:30)",
            Session: session);
    }

    private async Task<WhatsAppSelfServiceReply> HandleBookingTimeAsync(
        WhatsAppSelfServiceContext ctx, string text, CancellationToken ct)
    {
        if (!TryParseTime(text, out _))
        {
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitBookingTime,
                "Please send a valid time (e.g. 18:30 or 6:30 PM).");
        }

        var session = await LoadSessionAsync(ctx, ct);
        session.Time = text.Trim();
        var lines = new List<string> { "Which vehicle type?", "" };
        for (var i = 0; i < WhatsAppSelfServiceMenu.VehicleTypes.Count; i++)
            lines.Add($"{i + 1}. {WhatsAppSelfServiceMenu.VehicleTypes[i]}");

        return new WhatsAppSelfServiceReply(
            WhatsAppSelfServiceStates.AwaitBookingVehicleType,
            string.Join('\n', lines),
            Session: session,
            ListRows: WhatsAppSelfServiceMenu.VehicleTypes
                .Select((v, i) => new WhatsAppInteractiveRow($"ss_vt_{i + 1}", v))
                .ToList(),
            ListButtonLabel: "Vehicle");
    }

    private async Task<WhatsAppSelfServiceReply> HandleBookingVehicleAsync(
        WhatsAppSelfServiceContext ctx, string text, CancellationToken ct)
    {
        var matched = WhatsAppDemoBotOptions.MatchOption(WhatsAppSelfServiceMenu.VehicleTypes, text);
        if (matched is null
            && text.StartsWith("ss_vt_", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text.AsSpan(6), out var idx)
            && idx >= 1 && idx <= WhatsAppSelfServiceMenu.VehicleTypes.Count)
            matched = WhatsAppSelfServiceMenu.VehicleTypes[idx - 1];

        if (matched is null)
        {
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitBookingVehicleType,
                "Please choose a vehicle type from the list.");
        }

        var session = await LoadSessionAsync(ctx, ct);
        session.VehicleType = matched;
        return new WhatsAppSelfServiceReply(
            WhatsAppSelfServiceStates.AwaitBookingPassengers,
            "How many passengers?",
            Session: session);
    }

    private async Task<WhatsAppSelfServiceReply> HandleBookingPassengersAsync(
        WhatsAppSelfServiceContext ctx, string text, CancellationToken ct)
    {
        if (!int.TryParse(text.Trim(), out var pax) || pax < 1 || pax > 60)
        {
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitBookingPassengers,
                "Please enter a passenger count between 1 and 60.");
        }

        var session = await LoadSessionAsync(ctx, ct);
        session.Passengers = pax;
        return new WhatsAppSelfServiceReply(
            WhatsAppSelfServiceStates.AwaitBookingNotes,
            "Any notes for the driver? (or tap Skip)",
            Session: session,
            ReplyButtons:
            [
                new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.SkipNotes, "Skip"),
                new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.Agent, "Talk to Agent")
            ]);
    }

    private async Task<WhatsAppSelfServiceReply> HandleBookingNotesAsync(
        WhatsAppSelfServiceContext ctx, string text, CancellationToken ct)
    {
        var session = await LoadSessionAsync(ctx, ct);
        if (!text.Equals(WhatsAppSelfServiceMenu.SkipNotes, StringComparison.OrdinalIgnoreCase)
            && !text.Equals("Skip", StringComparison.OrdinalIgnoreCase))
            session.Notes = text.Trim();

        return await FinalizeBookingAsync(ctx, session, ct);
    }

    private async Task<WhatsAppSelfServiceReply> FinalizeBookingAsync(
        WhatsAppSelfServiceContext ctx,
        WhatsAppBotSessionData session,
        CancellationToken ct,
        string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(session.Pickup) || string.IsNullOrWhiteSpace(session.Destination))
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitBookingPickup,
                "Pickup and destination are required. " + WhatsAppSelfServiceCopy.AskPickup);

        if (!TryParseDate(session.Date, out var date) || !TryParseTime(session.Time, out var time))
            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.AwaitBookingDate,
                "Date or time was invalid. What date do you need?");

        var pickupAt = date.Date.Add(time);
        if (pickupAt <= DateTime.UtcNow.AddMinutes(-5) && pickupAt.Kind != DateTimeKind.Utc)
        {
            // Treat as local Pakistan-ish; compare as unspecified vs now
            if (pickupAt < DateTime.Now.AddMinutes(-5))
                return new WhatsAppSelfServiceReply(
                    WhatsAppSelfServiceStates.AwaitBookingDate,
                    "Pickup must be in the future. What date do you need?");
        }

        var pax = session.Passengers is > 0 and <= 60 ? session.Passengers.Value : 1;

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await repository.GetBookingIdForFlowSubmissionAsync(ctx.TenantId, idempotencyKey, ct);
            if (existing is > 0)
            {
                return new WhatsAppSelfServiceReply(
                    WhatsAppSelfServiceStates.MainMenu,
                    $"Booking already recorded (id {existing}).",
                    ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
                    ListButtonLabel: "Menu",
                    ClearSession: true);
            }
        }

        var customerId = await repository.EnsureCustomerIdForPhoneAsync(
            ctx.TenantId, ctx.PhoneE164, ctx.ProfileName, ct);
        if (customerId is null or <= 0)
            return FailBooking("We could not create a customer profile. Please talk to an agent.");

        await repository.LinkConversationCustomerAsync(ctx.TenantId, ctx.ConversationId, customerId.Value, ct);

        var routeId = options.Value.SelfServiceDefaultRouteId > 0
            ? options.Value.SelfServiceDefaultRouteId
            : await repository.GetDefaultRouteIdAsync(ctx.TenantId, ct) ?? 0;

        if (routeId <= 0)
            return FailBooking("Booking is temporarily unavailable. Please talk to an agent.");

        var notes = BuildBookingNotes(session);
        var amount = options.Value.SelfServiceDefaultAmount > 0
            ? options.Value.SelfServiceDefaultAmount
            : 1000m;

        try
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var inserted = await repository.TryInsertFlowSubmissionAsync(
                    ctx.TenantId, ctx.ConversationId, idempotencyKey, null, ct);
                if (!inserted)
                {
                    var dup = await repository.GetBookingIdForFlowSubmissionAsync(ctx.TenantId, idempotencyKey, ct);
                    return new WhatsAppSelfServiceReply(
                        WhatsAppSelfServiceStates.MainMenu,
                        dup is > 0 ? $"Booking already recorded (id {dup})." : "Duplicate request ignored.",
                        ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
                        ListButtonLabel: "Menu",
                        ClearSession: true);
                }
            }

            var result = await mediator.Send(new CreateBookingCommand(new CreateBookingDto(
                customerId.Value,
                routeId,
                DateTime.SpecifyKind(pickupAt, DateTimeKind.Unspecified),
                pax,
                amount,
                notes)), ct);

            if (!result.Success || result.Data <= 0)
                return FailBooking(result.Message ?? "Booking could not be created. Please try again or talk to an agent.");

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
                await repository.TryInsertFlowSubmissionAsync(
                    ctx.TenantId, ctx.ConversationId, idempotencyKey + ":done", result.Data, ct);

            var body =
                $"Booking Confirmed\n\nReference: #{result.Data}\nPickup: {session.Pickup}\nDestination: {session.Destination}\nDate: {session.Date}\nTime: {session.Time}\nVehicle: {session.VehicleType ?? "—"}\nPassengers: {pax}\nStatus: Pending\n\nOur team will confirm the final vehicle and fare.";

            return new WhatsAppSelfServiceReply(
                WhatsAppSelfServiceStates.MainMenu,
                body,
                ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
                ListButtonLabel: "Menu",
                ReplyButtons:
                [
                    new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.Agent, "Talk to Agent"),
                    new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.MenuAgain, "Main menu")
                ],
                ClearSession: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WhatsApp self-service booking failed");
            return FailBooking("Booking could not be created. Please try again or talk to an agent.");
        }
    }

    private async Task<WhatsAppSelfServiceReply> CompleteBookingFromFlowAsync(
        WhatsAppSelfServiceContext ctx, CancellationToken ct)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(ctx.FlowPayloadJson!);
            var root = doc.RootElement;
            var session = new WhatsAppBotSessionData
            {
                Pickup = GetFlowString(root, "pickup", "pickup_location"),
                Destination = GetFlowString(root, "destination", "dropoff", "dropoff_location"),
                Date = GetFlowString(root, "date", "pickup_date"),
                Time = GetFlowString(root, "time", "pickup_time"),
                VehicleType = GetFlowString(root, "vehicle_type", "vehicleType"),
                Notes = GetFlowString(root, "notes")
            };
            if (root.TryGetProperty("passengers", out var paxEl)
                && paxEl.TryGetInt32(out var pax))
                session.Passengers = pax;
            else if (int.TryParse(GetFlowString(root, "passenger_count", "passengers"), out var pax2))
                session.Passengers = pax2;

            return await FinalizeBookingAsync(ctx, session, ct, ctx.FlowIdempotencyKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid WhatsApp Flow payload");
            return FailBooking("We could not read your booking form. Please type MENU and try again.");
        }
    }

    private static string? GetFlowString(System.Text.Json.JsonElement root, params string[] names)
    {
        foreach (var n in names)
        {
            if (root.TryGetProperty(n, out var el))
            {
                var s = el.ValueKind == System.Text.Json.JsonValueKind.String
                    ? el.GetString()
                    : el.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
            }
        }
        return null;
    }

    private static WhatsAppSelfServiceReply FailBooking(string message)
        => new(
            WhatsAppSelfServiceStates.MainMenu,
            message,
            ListRows: WhatsAppSelfServiceMenu.MainMenuRows,
            ListButtonLabel: "Menu",
            ReplyButtons:
            [
                new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.Agent, "Talk to Agent"),
                new WhatsAppInteractiveRow(WhatsAppSelfServiceMenu.MenuAgain, "Main menu")
            ]);

    private async Task<WhatsAppBotSessionData> LoadSessionAsync(WhatsAppSelfServiceContext ctx, CancellationToken ct)
    {
        var json = await repository.GetBotSessionJsonAsync(ctx.TenantId, ctx.ConversationId, ct);
        return WhatsAppBotSessionData.Parse(json);
    }

    private static string BuildBookingNotes(WhatsAppBotSessionData s)
        => Truncate(
            $"[WhatsApp] Pickup: {s.Pickup}; Drop: {s.Destination}; Vehicle: {s.VehicleType}; Notes: {s.Notes}",
            900);

    private static bool TryParseDate(string? text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return DateTime.TryParse(text, System.Globalization.CultureInfo.GetCultureInfo("en-GB"),
                   System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date)
               || DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AllowWhiteSpaces, out date);
    }

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (TimeSpan.TryParse(text.Trim(), out time)) return true;
        if (DateTime.TryParse(text, out var dt))
        {
            time = dt.TimeOfDay;
            return true;
        }
        return false;
    }

    private static string TruncateBtn(string s) => s.Length <= 20 ? s : s[..20];
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
