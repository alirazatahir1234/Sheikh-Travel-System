using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Anonymous endpoints for the public customer booking portal.
/// </summary>
[ApiController]
[Route("api/customer-portal")]
[AllowAnonymous]
[EnableRateLimiting("portal")]
public class CustomerPortalController : ControllerBase
{
    private ISender? _mediator;
    private ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    [HttpGet("routes")]
    public async Task<IActionResult> GetRoutes()
        => Ok(await Mediator.Send(new GetPortalRoutesQuery()));

    [HttpGet("vehicles")]
    public async Task<IActionResult> GetVehicles()
        => Ok(await Mediator.Send(new GetPortalVehiclesQuery()));

    [HttpPost("price-estimate")]
    public async Task<IActionResult> PriceEstimate([FromBody] PortalPriceEstimateRequest body)
        => Ok(await Mediator.Send(new PortalPriceEstimateCommand(body)));

    [HttpGet("my-bookings")]
    public async Task<IActionResult> MyBookings([FromQuery] string phone)
        => Ok(await Mediator.Send(new GetPortalBookingsByPhoneQuery(phone)));

    [HttpGet("bookings/{id:int}")]
    public async Task<IActionResult> GetBooking(int id, [FromQuery] string phone)
        => Ok(await Mediator.Send(new GetPortalBookingDetailQuery(id, phone)));

    [HttpPost("bookings/{id:int}/payments")]
    public async Task<IActionResult> CreatePayment(int id, [FromBody] CreatePortalPaymentRequest body)
        => Ok(await Mediator.Send(new CreatePortalPaymentCommand(id, body)));

    [HttpPost("bookings")]
    public async Task<IActionResult> CreateBooking([FromBody] CreatePortalBookingRequest body)
        => Ok(await Mediator.Send(new CreatePortalBookingCommand(body)));
}
