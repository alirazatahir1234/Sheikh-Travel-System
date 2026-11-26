using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Commands;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.TravelNexus.Bookings.Queries;

namespace SheikhTravelSystem.API.Controllers.TravelNexus;

/// <summary>
/// Booking files (customer or Umrah group) with flight and hotel services, tickets, cancellations and refund tracking.
/// Status moves are validated by the booking / flight / hotel state machines; concurrent changes return 409.
/// </summary>
[Authorize]
[RequireModule(TenantModuleCatalog.TravelAgencyModuleCode)]
[RequirePermission(TravelPermissions.BookingView)]
[Route("api/travel/bookings")]
public class TravelBookingsController : BaseApiController
{
    /// <summary>Paged list. Filters: search (number, title, owner, PNR), status, ownerType, customerId, groupId, travelFrom/To, ticketingDue.</summary>
    [HttpGet]
    public async Task<IActionResult> GetBookings([FromQuery] GetTravelBookingsQuery query) => Ok(await Mediator.Send(query));

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await Mediator.Send(new GetTravelBookingSummaryQuery()));

    [HttpGet("by-customer/{customerId:int}")]
    public async Task<IActionResult> GetByCustomer(int customerId) => Ok(await Mediator.Send(new GetTravelBookingsByCustomerQuery(customerId)));

    [HttpGet("by-group/{groupId:int}")]
    public async Task<IActionResult> GetByGroup(int groupId) => Ok(await Mediator.Send(new GetTravelBookingsByGroupQuery(groupId)));

    /// <summary>File with flights (passports masked without ViewPII), hotels, cancellations, history, totals and allowed actions.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBooking(int id) => Ok(await Mediator.Send(new GetTravelBookingByIdQuery(id)));

    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTravelBookingDto dto) => Created(string.Empty, await Mediator.Send(new CreateTravelBookingCommand(dto)));

    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTravelBookingDto dto) => Ok(await Mediator.Send(new UpdateTravelBookingCommand(id, dto)));

    /// <summary>Complete | Reopen | Cancel (reason).</summary>
    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPost("{id:int}/transition")]
    public async Task<IActionResult> Transition(int id, [FromBody] TransitionTravelBookingDto dto)
        => Ok(await Mediator.Send(new TransitionTravelBookingCommand(id, dto)));

    // ── Flights ──
    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPost("{id:int}/flights")]
    public async Task<IActionResult> AddFlight(int id, [FromBody] SaveTravelFlightDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelFlightCommand(id, null, dto)));

    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPut("{id:int}/flights/{flightId:int}")]
    public async Task<IActionResult> UpdateFlight(int id, int flightId, [FromBody] SaveTravelFlightDto dto)
        => Ok(await Mediator.Send(new SaveTravelFlightCommand(id, flightId, dto)));

    /// <summary>Hold (PNR + ticketing time limit) | Confirm | Ticket.</summary>
    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPost("{id:int}/flights/{flightId:int}/transition")]
    public async Task<IActionResult> TransitionFlight(int id, int flightId, [FromBody] TransitionTravelFlightDto dto)
        => Ok(await Mediator.Send(new TransitionTravelFlightCommand(id, flightId, dto)));

    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPut("{id:int}/flights/{flightId:int}/tickets")]
    public async Task<IActionResult> SetTickets(int id, int flightId, [FromBody] SetTravelTicketsDto dto)
        => Ok(await Mediator.Send(new SetTravelTicketsCommand(id, flightId, dto)));

    [RequirePermission(TravelPermissions.BookingCancel)]
    [HttpPost("{id:int}/flights/{flightId:int}/cancel")]
    public async Task<IActionResult> CancelFlight(int id, int flightId, [FromBody] CancelTravelServiceDto dto)
        => Ok(await Mediator.Send(new CancelTravelFlightCommand(id, flightId, dto)));

    [RequirePermission(TravelPermissions.BookingCancel)]
    [HttpPost("{id:int}/flights/{flightId:int}/passengers/{passengerId:int}/cancel")]
    public async Task<IActionResult> CancelPassenger(int id, int flightId, int passengerId, [FromBody] CancelTravelServiceDto dto)
        => Ok(await Mediator.Send(new CancelTravelFlightPassengerCommand(id, flightId, passengerId, dto)));

    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpDelete("{id:int}/flights/{flightId:int}")]
    public async Task<IActionResult> DeleteFlight(int id, int flightId) => Ok(await Mediator.Send(new DeleteTravelFlightCommand(id, flightId)));

    // ── Hotels ──
    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPost("{id:int}/hotels")]
    public async Task<IActionResult> AddHotel(int id, [FromBody] SaveTravelHotelBookingDto dto)
        => Created(string.Empty, await Mediator.Send(new SaveTravelHotelBookingCommand(id, null, dto)));

    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPut("{id:int}/hotels/{hotelBookingId:int}")]
    public async Task<IActionResult> UpdateHotel(int id, int hotelBookingId, [FromBody] SaveTravelHotelBookingDto dto)
        => Ok(await Mediator.Send(new SaveTravelHotelBookingCommand(id, hotelBookingId, dto)));

    /// <summary>Confirm (confirmation number) | CheckIn | CheckOut | NoShow.</summary>
    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpPost("{id:int}/hotels/{hotelBookingId:int}/transition")]
    public async Task<IActionResult> TransitionHotel(int id, int hotelBookingId, [FromBody] TransitionTravelHotelDto dto)
        => Ok(await Mediator.Send(new TransitionTravelHotelBookingCommand(id, hotelBookingId, dto)));

    [RequirePermission(TravelPermissions.BookingCancel)]
    [HttpPost("{id:int}/hotels/{hotelBookingId:int}/cancel")]
    public async Task<IActionResult> CancelHotel(int id, int hotelBookingId, [FromBody] CancelTravelServiceDto dto)
        => Ok(await Mediator.Send(new CancelTravelHotelBookingCommand(id, hotelBookingId, dto)));

    [RequirePermission(TravelPermissions.BookingManage)]
    [HttpDelete("{id:int}/hotels/{hotelBookingId:int}")]
    public async Task<IActionResult> DeleteHotel(int id, int hotelBookingId) => Ok(await Mediator.Send(new DeleteTravelHotelBookingCommand(id, hotelBookingId)));

    // ── Refund tracking ──
    [RequirePermission(TravelPermissions.BookingCancel)]
    [HttpPut("{id:int}/cancellations/{cancellationId:int}/refund-status")]
    public async Task<IActionResult> UpdateRefundStatus(int id, int cancellationId, [FromBody] UpdateTravelRefundStatusDto dto)
        => Ok(await Mediator.Send(new UpdateTravelRefundStatusCommand(id, cancellationId, dto)));
}
