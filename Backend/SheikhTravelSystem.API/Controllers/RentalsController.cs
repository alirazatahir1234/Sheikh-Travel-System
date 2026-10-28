using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Rentals.Commands;
using SheikhTravelSystem.Application.Features.Rentals.DTOs;
using SheikhTravelSystem.Application.Features.Rentals.Queries;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequireModule("RENTAL")]
[RequirePermission(RentalPermissions.View)]
public class RentalsController : BaseApiController
{
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings([FromQuery] GetRentalBookingsQuery query)
        => Ok(await Mediator.Send(query));

    [HttpGet("bookings/{id:int}")]
    public async Task<IActionResult> GetBooking(int id)
        => Ok(await Mediator.Send(new GetRentalBookingByIdQuery(id)));

    [HttpGet("bookings/{id:int}/payments")]
    public async Task<IActionResult> GetPayments(int id)
        => Ok(await Mediator.Send(new GetRentalPaymentsQuery(id)));

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPost("bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] CreateRentalBookingDto dto)
    {
        var result = await Mediator.Send(new CreateRentalBookingCommand(dto));
        return Created(string.Empty, result);
    }

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPut("bookings/{id:int}")]
    public async Task<IActionResult> UpdateBooking(int id, [FromBody] UpdateRentalBookingDto dto)
        => Ok(await Mediator.Send(new UpdateRentalBookingCommand(id, dto)));

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPost("bookings/{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id)
        => Ok(await Mediator.Send(new ConfirmRentalBookingCommand(id)));

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPost("bookings/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
        => Ok(await Mediator.Send(new CancelRentalBookingCommand(id)));

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPost("bookings/{id:int}/pickup")]
    public async Task<IActionResult> Pickup(int id, [FromBody] PickupRentalDto dto)
        => Ok(await Mediator.Send(new PickupRentalCommand(id, dto)));

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPost("bookings/{id:int}/return")]
    public async Task<IActionResult> Return(int id, [FromBody] ReturnRentalDto dto)
        => Ok(await Mediator.Send(new ReturnRentalCommand(id, dto)));

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPost("bookings/{id:int}/close")]
    public async Task<IActionResult> Close(int id)
        => Ok(await Mediator.Send(new CloseRentalBookingCommand(id)));

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPost("bookings/{id:int}/agreement/sign")]
    public async Task<IActionResult> SignAgreement(int id, [FromBody] SignRentalAgreementDto dto)
        => Ok(await Mediator.Send(new SignRentalAgreementCommand(id, dto)));

    [RequirePermission(RentalPermissions.Manage)]
    [HttpPost("bookings/{id:int}/payments")]
    public async Task<IActionResult> RecordPayment(int id, [FromBody] RecordRentalPaymentDto dto)
    {
        var result = await Mediator.Send(new RecordRentalPaymentCommand(id, dto));
        return Created(string.Empty, result);
    }

    [HttpGet("rates")]
    public async Task<IActionResult> GetRates([FromQuery] GetRentalRatesQuery query)
        => Ok(await Mediator.Send(query));

    [RequirePermission(RentalPermissions.Rates)]
    [HttpPost("rates")]
    public async Task<IActionResult> CreateRate([FromBody] CreateRentalRateDto dto)
    {
        var result = await Mediator.Send(new CreateRentalRateCommand(dto));
        return Created(string.Empty, result);
    }

    [RequirePermission(RentalPermissions.Rates)]
    [HttpPut("rates/{id:int}")]
    public async Task<IActionResult> UpdateRate(int id, [FromBody] UpdateRentalRateDto dto)
        => Ok(await Mediator.Send(new UpdateRentalRateCommand(id, dto)));

    [RequirePermission(RentalPermissions.Rates)]
    [HttpDelete("rates/{id:int}")]
    public async Task<IActionResult> DeleteRate(int id)
        => Ok(await Mediator.Send(new DeleteRentalRateCommand(id)));

    [HttpGet("availability")]
    public async Task<IActionResult> Availability([FromQuery] CheckRentalAvailabilityQuery query)
        => Ok(await Mediator.Send(query));

    [HttpGet("quote")]
    public async Task<IActionResult> Quote([FromQuery] QuoteRentalQuery query)
        => Ok(await Mediator.Send(query));
}
