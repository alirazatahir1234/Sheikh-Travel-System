using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Suppliers.DTOs;
using F = SheikhTravelSystem.Application.Features.TravelNexus.Bookings.TravelFlightStateMachine;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Commands;

/// <summary>Pure segment checks and local → UTC conversion.</summary>
public static partial class TravelFlightPlanner
{
    public static (IReadOnlyList<TravelFlightSegmentWrite> Segments, string? Error) Plan(
        IReadOnlyList<SaveTravelFlightSegmentDto> segments, IReadOnlyList<TravelAirportDto> airports)
    {
        var byCode = airports.ToDictionary(a => a.IataCode, StringComparer.Ordinal);
        var writes = new List<TravelFlightSegmentWrite>();
        DateTime? previousArrival = null;
        for (var i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            var label = $"Segment {i + 1}";
            if (!byCode.TryGetValue(s.FromAirport, out var from)) return ([], $"{label}: unknown airport {s.FromAirport}.");
            if (!byCode.TryGetValue(s.ToAirport, out var to)) return ([], $"{label}: unknown airport {s.ToAirport}.");
            var fromZone = TravelFlightTimes.FindZone(from.IanaTimeZone);
            var toZone = TravelFlightTimes.FindZone(to.IanaTimeZone);
            if (fromZone is null || toZone is null) return ([], $"{label}: the server cannot resolve the airport time zone.");
            var departUtc = TravelFlightTimes.ToUtc(s.DepartLocal, fromZone);
            var arriveUtc = TravelFlightTimes.ToUtc(s.ArriveLocal, toZone);
            if (departUtc is null) return ([], $"{label}: {s.DepartLocal:yyyy-MM-dd HH:mm} does not exist in {s.FromAirport} local time (clock change).");
            if (arriveUtc is null) return ([], $"{label}: {s.ArriveLocal:yyyy-MM-dd HH:mm} does not exist in {s.ToAirport} local time (clock change).");
            if (arriveUtc < departUtc) return ([], $"{label}: arrival is before departure once time zones are applied.");
            if (arriveUtc - departUtc > TimeSpan.FromHours(24)) return ([], $"{label}: a single flight cannot last more than 24 hours.");
            if (previousArrival is DateTime prev && departUtc < prev) return ([], $"{label} departs before the previous segment arrives.");
            previousArrival = arriveUtc;
            writes.Add(new TravelFlightSegmentWrite(i + 1, s, departUtc.Value, arriveUtc.Value));
        }
        return (writes, null);
    }

    /// <summary>Ticket numbers are 13 digits (airline prefix + serial); dashes and spaces are ignored.</summary>
    public static string? NormalizeTicket(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = value.Replace("-", "").Replace(" ", "");
        return TicketRegex().IsMatch(digits) ? digits : null;
    }

    public static string NormalizePnr(string pnr) => pnr.Trim().ToUpperInvariant();

    [GeneratedRegex("^[0-9]{13}$")]
    private static partial Regex TicketRegex();
}

public class SaveTravelFlightSegmentValidator : AbstractValidator<SaveTravelFlightSegmentDto>
{
    public SaveTravelFlightSegmentValidator()
    {
        RuleFor(x => x.AirlineCode).NotEmpty().Matches("^[A-Z0-9]{2,3}$").WithMessage("Airline code must be 2–3 characters (e.g. PK).");
        RuleFor(x => x.FlightNumber).NotEmpty().Matches("^[0-9]{1,4}[A-Z]?$").WithMessage("Flight number: up to 4 digits, optional letter.");
        RuleFor(x => x.FromAirport).NotEmpty().Matches("^[A-Z]{3}$");
        RuleFor(x => x.ToAirport).NotEmpty().Matches("^[A-Z]{3}$").NotEqual(x => x.FromAirport).WithMessage("Origin and destination must differ.");
        RuleFor(x => x.CabinClass).Must(c => TravelValues.IsOneOf(c, TravelValues.CabinClasses)).WithMessage("Unknown cabin class.");
        RuleFor(x => x.BookingClass).Matches("^[A-Z]$").When(x => !string.IsNullOrEmpty(x.BookingClass));
        RuleFor(x => x.Baggage).MaximumLength(30);
    }
}

// ── Save (create / update) ──────────────────────────────────────────────

/// <summary>Adds (FlightId null) or edits a flight. Passengers are fixed once ticketed.</summary>
public record SaveTravelFlightCommand(int BookingId, int? FlightId, SaveTravelFlightDto Flight) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => FlightId.HasValue ? "UpdateFlight" : "AddFlight";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class SaveTravelFlightCommandValidator : AbstractValidator<SaveTravelFlightCommand>
{
    public SaveTravelFlightCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.Flight).NotNull();
        RuleFor(x => x.Flight.Direction).Must(d => TravelValues.IsOneOf(d, TravelValues.FlightDirections)).WithMessage("Unknown direction.");
        RuleFor(x => x.Flight.Pnr).Matches("^[A-Za-z0-9]{5,8}$").When(x => !string.IsNullOrEmpty(x.Flight.Pnr)).WithMessage("PNR: 5–8 letters or digits.");
        RuleFor(x => x.Flight.AirlinePnr).Matches("^[A-Za-z0-9]{5,8}$").When(x => !string.IsNullOrEmpty(x.Flight.AirlinePnr));
        RuleFor(x => x.Flight.Notes).MaximumLength(1000);
        RuleFor(x => x.Flight.Segments).NotNull().Must(s => s.Count <= 12).WithMessage("At most 12 segments.");
        RuleForEach(x => x.Flight.Segments).SetValidator(new SaveTravelFlightSegmentValidator());
        RuleFor(x => x.Flight.Passengers).NotNull().Must(p => p.Count <= 500)
            .Must(p => p.Select(x => x.TravelerId).Distinct().Count() == p.Count).WithMessage("A traveler can be on a flight once.");
        RuleForEach(x => x.Flight.Passengers).ChildRules(p =>
            p.RuleFor(x => x.PassengerType).Must(t => TravelValues.IsOneOf(t, TravelValues.PassengerTypes)).WithMessage("Unknown passenger type."));
    }
}

public class SaveTravelFlightCommandHandler(ITravelBookingRepository repository, ITravelConfigRepository config, TravelBookingGuard guard,
    ITenantContext tenantContext, ICurrentUserService currentUser) : IRequestHandler<SaveTravelFlightCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(SaveTravelFlightCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<int>.FailResponse(TravelBookingGuard.NotOpenMessage);

        var dto = request.Flight with
        {
            Pnr = string.IsNullOrWhiteSpace(request.Flight.Pnr) ? null : TravelFlightPlanner.NormalizePnr(request.Flight.Pnr),
            AirlinePnr = string.IsNullOrWhiteSpace(request.Flight.AirlinePnr) ? null : TravelFlightPlanner.NormalizePnr(request.Flight.AirlinePnr),
            CurrencyCode = string.IsNullOrWhiteSpace(request.Flight.CurrencyCode) ? null : request.Flight.CurrencyCode.Trim().ToUpperInvariant(),
            Notes = string.IsNullOrWhiteSpace(request.Flight.Notes) ? null : request.Flight.Notes.Trim()
        };

        var replacePassengers = true;
        if (request.FlightId is int flightId)
        {
            var flight = await repository.GetFlightStateAsync(file.Id, flightId, tenantId, cancellationToken) ?? throw new NotFoundException("TravelFlightBooking", flightId);
            if (!F.CanEdit(flight.Status)) return ApiResponse<int>.FailResponse("A cancelled flight cannot be edited.");
            replacePassengers = F.CanEditPassengers(flight.Status);
            var current = flight.Passengers.Select(p => p.TravelerId).ToHashSet();
            var requested = dto.Passengers.Select(p => p.TravelerId).ToHashSet();
            if (!replacePassengers && !current.SetEquals(requested))
                return ApiResponse<int>.FailResponse("Passengers cannot change once the flight is ticketed — cancel a passenger's ticket instead.");
            var removedWithTicket = flight.Passengers.Where(p => !requested.Contains(p.TravelerId) && p.TicketStatus != "NotIssued").ToList();
            if (removedWithTicket.Count > 0) return ApiResponse<int>.FailResponse("A passenger with an issued ticket cannot be removed — cancel the ticket instead.");
            if (flight.Status is "Confirmed" or "Ticketed" && dto.Pnr is null) return ApiResponse<int>.FailResponse("A confirmed flight needs its PNR.");
        }

        var amountError = TravelBookingRules.ValidateAmounts(dto.SellAmount, dto.CostAmount, dto.CurrencyCode);
        if (amountError is not null) return ApiResponse<int>.FailResponse(amountError);
        if (dto.SupplierId is int supplierId && !await repository.SupplierExistsAsync(tenantId, supplierId, cancellationToken))
            return ApiResponse<int>.FailResponse("Supplier not found.");
        if (dto.Pnr is not null && await repository.PnrInUseAsync(tenantId, dto.Pnr, request.FlightId, cancellationToken))
            return ApiResponse<int>.FailResponse($"PNR {dto.Pnr} is already used by another flight.");

        var eligible = (await repository.GetEligibleTravelersAsync(tenantId, file.CustomerId, file.GroupId, cancellationToken))
            .ToDictionary(e => e.TravelerId, e => e.GroupMemberId);
        if (dto.Passengers.Any(p => !eligible.ContainsKey(p.TravelerId)))
            return ApiResponse<int>.FailResponse(file.GroupId is null
                ? "Passengers must be travelers of the booking's customer."
                : "Passengers must be active pilgrims of the group.");
        dto = dto with { Passengers = dto.Passengers.Select(p => p with { GroupMemberId = eligible[p.TravelerId] }).ToList() };

        var codes = dto.Segments.SelectMany(s => new[] { s.FromAirport, s.ToAirport }).Distinct().ToList();
        var airports = codes.Count == 0 ? [] : await config.GetAirportsAsync(null, codes, cancellationToken);
        var (segments, segmentError) = TravelFlightPlanner.Plan(dto.Segments, airports);
        if (segmentError is not null) return ApiResponse<int>.FailResponse(segmentError);

        var id = await repository.SaveFlightAsync(tenantId, file.Id, request.FlightId, dto, segments, replacePassengers, currentUser.UserId, cancellationToken);
        if (id == 0) throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<int>.SuccessResponse(id, request.FlightId.HasValue ? "Flight updated." : "Flight added.");
    }
}

// ── Transition (hold / confirm / ticket) ────────────────────────────────

public record TransitionTravelFlightCommand(int BookingId, int FlightId, TransitionTravelFlightDto Transition) : IRequest<ApiResponse<string>>, IAuditableCommand
{
    public string AuditAction => $"Flight{Transition.Action}";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class TransitionTravelFlightCommandValidator : AbstractValidator<TransitionTravelFlightCommand>
{
    public TransitionTravelFlightCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.FlightId).GreaterThan(0);
        RuleFor(x => x.Transition.Action).Must(a => TravelValues.IsOneOf(a, F.TransitionActions)).WithMessage("Unknown action.");
        RuleFor(x => x.Transition.Pnr).Matches("^[A-Za-z0-9]{5,8}$").When(x => !string.IsNullOrEmpty(x.Transition.Pnr)).WithMessage("PNR: 5–8 letters or digits.");
        RuleFor(x => x.Transition.TicketingTimeLimitUtc).NotNull().When(x => x.Transition.Action == F.Actions.Hold)
            .WithMessage("A hold needs the ticketing time limit.");
        RuleFor(x => x.Transition.Note).MaximumLength(500);
    }
}

public class TransitionTravelFlightCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<TransitionTravelFlightCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(TransitionTravelFlightCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<string>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var flight = await repository.GetFlightStateAsync(file.Id, request.FlightId, tenantId, cancellationToken)
                     ?? throw new NotFoundException("TravelFlightBooking", request.FlightId);

        var t = request.Transition;
        var target = F.Target(flight.Status, t.Action);
        if (target is null) return ApiResponse<string>.FailResponse($"A {flight.Status} flight cannot be moved with '{t.Action}'.");

        var pnr = string.IsNullOrWhiteSpace(t.Pnr) ? flight.Pnr : TravelFlightPlanner.NormalizePnr(t.Pnr);
        switch (t.Action)
        {
            case F.Actions.Hold:
                if (pnr is null) return ApiResponse<string>.FailResponse("A hold needs the PNR.");
                if (t.TicketingTimeLimitUtc <= DateTime.UtcNow) return ApiResponse<string>.FailResponse("The ticketing time limit must be in the future.");
                break;
            case F.Actions.Confirm:
                if (pnr is null) return ApiResponse<string>.FailResponse("Confirmation needs the PNR.");
                break;
            case F.Actions.Ticket:
                if (flight.SegmentCount == 0) return ApiResponse<string>.FailResponse("Add the flight segments before ticketing.");
                if (flight.Passengers.Count == 0) return ApiResponse<string>.FailResponse("Add passengers before ticketing.");
                var missing = flight.Passengers.Count(p => p.TicketStatus != "Issued");
                if (missing > 0) return ApiResponse<string>.FailResponse($"{missing} passenger(s) have no issued ticket number.");
                break;
        }
        if (pnr != flight.Pnr && pnr is not null && await repository.PnrInUseAsync(tenantId, pnr, flight.Id, cancellationToken))
            return ApiResponse<string>.FailResponse($"PNR {pnr} is already used by another flight.");

        if (!await repository.TransitionFlightAsync(tenantId, file.Id, flight.Id, flight.Status, target, pnr,
                t.Action == F.Actions.Hold ? t.TicketingTimeLimitUtc : null, t.Note?.Trim(), currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<string>.SuccessResponse(target, $"Flight {target.ToLowerInvariant()}.");
    }
}

// ── Tickets ─────────────────────────────────────────────────────────────

/// <summary>Records (or re-issues) ticket numbers for passengers of a confirmed or ticketed flight.</summary>
public record SetTravelTicketsCommand(int BookingId, int FlightId, SetTravelTicketsDto Tickets) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "IssueTickets";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class SetTravelTicketsCommandValidator : AbstractValidator<SetTravelTicketsCommand>
{
    public SetTravelTicketsCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.FlightId).GreaterThan(0);
        RuleFor(x => x.Tickets.Tickets).NotEmpty().Must(t => t.Select(x => x.PassengerId).Distinct().Count() == t.Count)
            .WithMessage("Each passenger once.");
        RuleForEach(x => x.Tickets.Tickets).Must(t => TravelFlightPlanner.NormalizeTicket(t.TicketNumber) is not null)
            .WithMessage("Ticket numbers are 13 digits (e.g. 214-2345678901).");
    }
}

public class SetTravelTicketsCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<SetTravelTicketsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(SetTravelTicketsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<bool>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var flight = await repository.GetFlightStateAsync(file.Id, request.FlightId, tenantId, cancellationToken)
                     ?? throw new NotFoundException("TravelFlightBooking", request.FlightId);
        if (!F.CanIssueTickets(flight.Status)) return ApiResponse<bool>.FailResponse("Tickets are issued once the flight is confirmed.");

        var tickets = request.Tickets.Tickets.Select(t => new TravelTicketNumberDto(t.PassengerId, TravelFlightPlanner.NormalizeTicket(t.TicketNumber)!)).ToList();
        if (tickets.Select(t => t.TicketNumber).Distinct().Count() != tickets.Count) return ApiResponse<bool>.FailResponse("Two passengers cannot share a ticket number.");
        var byId = flight.Passengers.ToDictionary(p => p.Id);
        foreach (var t in tickets)
        {
            if (!byId.TryGetValue(t.PassengerId, out var p)) throw new NotFoundException("TravelFlightPassenger", t.PassengerId);
            if (p.TicketStatus is "Voided" or "Refunded") return ApiResponse<bool>.FailResponse("A voided or refunded ticket cannot be changed.");
        }
        var inUse = await repository.TicketNumbersInUseAsync(tenantId, tickets.Select(t => t.TicketNumber).ToList(), flight.Id, cancellationToken);
        if (inUse.Count > 0) return ApiResponse<bool>.FailResponse($"Ticket number already used: {string.Join(", ", inUse)}.");

        if (!await repository.SetTicketsAsync(tenantId, file.Id, flight.Id, flight.Status, tickets, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Ticket numbers saved.");
    }
}

// ── Cancel flight / passenger ───────────────────────────────────────────

public class CancelTravelServiceDtoValidator : AbstractValidator<CancelTravelServiceDto>
{
    public CancelTravelServiceDtoValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.TicketOutcome).Must(o => o is null or "Voided" or "Refunded").WithMessage("Ticket outcome is Voided or Refunded.");
    }
}

/// <summary>Cancels a whole flight; issued tickets become Voided or Refunded. Records penalties and refunds.</summary>
public record CancelTravelFlightCommand(int BookingId, int FlightId, CancelTravelServiceDto Cancellation) : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CancelFlight";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class CancelTravelFlightCommandValidator : AbstractValidator<CancelTravelFlightCommand>
{
    public CancelTravelFlightCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.FlightId).GreaterThan(0);
        RuleFor(x => x.Cancellation).NotNull().SetValidator(new CancelTravelServiceDtoValidator());
    }
}

public class CancelTravelFlightCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<CancelTravelFlightCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CancelTravelFlightCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<int>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var flight = await repository.GetFlightStateAsync(file.Id, request.FlightId, tenantId, cancellationToken)
                     ?? throw new NotFoundException("TravelFlightBooking", request.FlightId);
        if (!F.IsOpen(flight.Status)) return ApiResponse<int>.FailResponse("The flight is already cancelled.");

        var c = request.Cancellation;
        if (flight.Passengers.Any(p => p.TicketStatus == "Issued") && c.TicketOutcome is null)
            return ApiResponse<int>.FailResponse("Issued tickets exist — choose whether they are voided or refunded.");
        var error = TravelBookingRules.ValidateCancellation(c, flight.SellAmount, flight.CostAmount, flight.CurrencyCode);
        if (error is not null) return ApiResponse<int>.FailResponse(error);

        var id = await repository.CancelFlightAsync(tenantId, file.Id, flight.Id, flight.Status, c.TicketOutcome ?? "Voided",
            CancellationWrite.From(c, flight.CurrencyCode), currentUser.UserId, cancellationToken)
            ?? throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<int>.SuccessResponse(id, "Flight cancelled.");
    }
}

/// <summary>Voids or refunds one passenger's ticket on a ticketed flight.</summary>
public record CancelTravelFlightPassengerCommand(int BookingId, int FlightId, int PassengerId, CancelTravelServiceDto Cancellation)
    : IRequest<ApiResponse<int>>, IAuditableCommand
{
    public string AuditAction => "CancelPassenger";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class CancelTravelFlightPassengerCommandValidator : AbstractValidator<CancelTravelFlightPassengerCommand>
{
    public CancelTravelFlightPassengerCommandValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
        RuleFor(x => x.FlightId).GreaterThan(0);
        RuleFor(x => x.PassengerId).GreaterThan(0);
        RuleFor(x => x.Cancellation).NotNull().SetValidator(new CancelTravelServiceDtoValidator());
        RuleFor(x => x.Cancellation.TicketOutcome).NotEmpty().WithMessage("Choose whether the ticket is voided or refunded.");
    }
}

public class CancelTravelFlightPassengerCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<CancelTravelFlightPassengerCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CancelTravelFlightPassengerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<int>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var flight = await repository.GetFlightStateAsync(file.Id, request.FlightId, tenantId, cancellationToken)
                     ?? throw new NotFoundException("TravelFlightBooking", request.FlightId);
        var passenger = flight.Passengers.FirstOrDefault(p => p.Id == request.PassengerId) ?? throw new NotFoundException("TravelFlightPassenger", request.PassengerId);
        if (!F.CanCancelPassenger(flight.Status)) return ApiResponse<int>.FailResponse("Only passengers of a ticketed flight are cancelled this way — edit the passenger list instead.");
        if (passenger.TicketStatus != "Issued") return ApiResponse<int>.FailResponse("The passenger's ticket is not issued.");

        var c = request.Cancellation;
        var error = TravelBookingRules.ValidateCancellation(c, flight.SellAmount, flight.CostAmount, flight.CurrencyCode);
        if (error is not null) return ApiResponse<int>.FailResponse(error);

        var id = await repository.CancelPassengerAsync(tenantId, file.Id, flight.Id, passenger.Id, c.TicketOutcome!,
            CancellationWrite.From(c, flight.CurrencyCode), currentUser.UserId, cancellationToken)
            ?? throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<int>.SuccessResponse(id, $"Ticket {c.TicketOutcome!.ToLowerInvariant()}.");
    }
}

/// <summary>Removes a flight that is still only requested (nothing booked with the airline).</summary>
public record DeleteTravelFlightCommand(int BookingId, int FlightId) : IRequest<ApiResponse<bool>>, IAuditableCommand
{
    public string AuditAction => "DeleteFlight";
    public string AuditEntityName => "TravelBooking";
    public int? AuditEntityId => BookingId;
}

public class DeleteTravelFlightCommandHandler(ITravelBookingRepository repository, TravelBookingGuard guard, ITenantContext tenantContext,
    ICurrentUserService currentUser) : IRequestHandler<DeleteTravelFlightCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteTravelFlightCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var file = await guard.GetVisibleAsync(request.BookingId, tenantId, cancellationToken);
        if (!TravelBookingStateMachine.IsOpen(file.Status)) return ApiResponse<bool>.FailResponse(TravelBookingGuard.NotOpenMessage);
        var flight = await repository.GetFlightStateAsync(file.Id, request.FlightId, tenantId, cancellationToken)
                     ?? throw new NotFoundException("TravelFlightBooking", request.FlightId);
        if (flight.Status != "Requested") return ApiResponse<bool>.FailResponse("Only a requested flight can be removed — cancel it instead.");
        if (!await repository.DeleteFlightAsync(tenantId, file.Id, flight.Id, currentUser.UserId, cancellationToken))
            throw new ConflictException(TravelBookingGuard.ConflictMessage);
        return ApiResponse<bool>.SuccessResponse(true, "Flight removed.");
    }
}

internal static class CancellationWrite
{
    public static TravelServiceCancellationWrite From(CancelTravelServiceDto c, string? currency)
        => new(c.Reason.Trim(), c.SupplierPenalty, c.CustomerPenalty, c.RefundFromSupplier, c.RefundToCustomer, currency,
            TravelRefundStateMachine.Initial(c.RefundFromSupplier, c.RefundToCustomer));
}
