using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Application.Features.CustomerPortal.DTOs;
using SheikhTravelSystem.Application.Features.CustomerPortal.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Public customer booking portal API.
/// </summary>
[ApiController]
[Route("api/customer-portal")]
[EnableRateLimiting("portal")]
public class CustomerPortalController : ControllerBase
{
    private ISender? _mediator;
    private ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    private string? PortalPhone => PortalUserContext.GetPhone(User);

    private IActionResult? RequirePortalPhone(out string phone)
    {
        phone = PortalPhone ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Unauthorized(new { success = false, message = "Portal sign-in required." });
        }

        return null;
    }

    private IActionResult? RequirePortalContext(out string phone, out int? customerId)
    {
        phone = PortalPhone ?? string.Empty;
        customerId = PortalUserContext.GetCustomerId(User);
        if (string.IsNullOrWhiteSpace(phone))
        {
            return Unauthorized(new { success = false, message = "Portal sign-in required." });
        }

        return null;
    }

    [HttpGet("routes")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRoutes()
        => Ok(await Mediator.Send(new GetPortalRoutesQuery()));

    [HttpGet("vehicles")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVehicles()
        => Ok(await Mediator.Send(new GetPortalVehiclesQuery()));

    [HttpPost("price-estimate")]
    [AllowAnonymous]
    public async Task<IActionResult> PriceEstimate([FromBody] PortalPriceEstimateRequest body)
        => Ok(await Mediator.Send(new PortalPriceEstimateCommand(body)));

    [HttpPost("quote")]
    [AllowAnonymous]
    public async Task<IActionResult> PointToPointQuote([FromBody] PortalPointToPointQuoteRequest body)
        => Ok(await Mediator.Send(new PortalPointToPointQuoteCommand(body)));

    [HttpPost("promo/validate")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidatePromo([FromBody] PortalValidatePromoRequest body)
        => Ok(await Mediator.Send(new ValidatePortalPromoCommand("guest", body)));

    [HttpGet("addresses")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetAddresses()
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalSavedAddressesQuery(phone)));
    }

    [HttpPost("addresses")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> SaveAddress([FromBody] PortalSaveAddressRequest body)
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new SavePortalAddressCommand(phone, body)));
    }

    [HttpGet("favorite-routes")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetFavoriteRoutes()
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalFavoriteRoutesQuery(phone)));
    }

    [HttpPost("favorite-routes")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> AddFavoriteRoute([FromBody] AddPortalFavoriteRouteRequest body)
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new AddPortalFavoriteRouteCommand(phone, body.RouteId, body.Label)));
    }

    [HttpGet("notifications")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetNotifications()
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalCustomerNotificationsQuery(phone)));
    }

    [HttpGet("vehicles/{vehicleId:int}/seats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVehicleSeats(int vehicleId, [FromQuery] DateTime pickupTime)
        => Ok(await Mediator.Send(new GetPortalVehicleSeatsQuery(vehicleId, pickupTime)));

    [HttpGet("loyalty")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetLoyalty()
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalLoyaltyQuery(phone)));
    }

    [HttpGet("wallet")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetWallet()
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalWalletQuery(phone)));
    }

    [HttpPost("bookings")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateBooking([FromBody] CreatePortalBookingRequest body)
    {
        if (!string.IsNullOrWhiteSpace(PortalPhone))
        {
            body = body with { Phone = PortalPhone };
        }

        return Ok(await Mediator.Send(new CreatePortalBookingCommand(body)));
    }

    [HttpGet("my-bookings")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> MyBookings()
    {
        var denied = RequirePortalContext(out var phone, out var customerId);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalBookingsByPhoneQuery(phone, customerId)));
    }

    [HttpGet("bookings/{id:int}")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetBooking(int id)
    {
        var denied = RequirePortalContext(out var phone, out var customerId);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalBookingDetailQuery(id, phone, customerId)));
    }

    [HttpGet("bookings/{id:int}/tracking")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetBookingTracking(int id)
    {
        var denied = RequirePortalContext(out var phone, out var customerId);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalBookingTrackingQuery(id, phone, customerId)));
    }

    [HttpGet("bookings/{id:int}/invoice")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetBookingInvoice(int id)
    {
        var denied = RequirePortalContext(out var phone, out var customerId);
        if (denied is not null) return denied;
        var result = await Mediator.Send(new GetPortalBookingInvoiceQuery(id, phone, customerId));
        if (!result.Success || result.Data is null)
        {
            return BadRequest(result);
        }

        return File(result.Data, "text/html", $"invoice-{id}.html");
    }

    [HttpPost("bookings/{id:int}/cancel")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var denied = RequirePortalContext(out var phone, out var customerId);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new CancelPortalBookingCommand(id, phone, customerId)));
    }

    [HttpPost("bookings/{id:int}/payments")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> CreatePayment(int id, [FromBody] CreatePortalPaymentRequest body)
    {
        var denied = RequirePortalContext(out var phone, out var customerId);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new CreatePortalPaymentCommand(id, phone, body, customerId)));
    }

    [HttpGet("notifications/preferences")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> GetNotificationPreferences()
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new GetPortalNotificationPreferencesQuery(phone)));
    }

    [HttpPut("notifications/preferences")]
    [Authorize(Roles = PortalUserContext.PortalRole)]
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdatePortalNotificationPreferencesRequest body)
    {
        var denied = RequirePortalPhone(out var phone);
        if (denied is not null) return denied;
        return Ok(await Mediator.Send(new UpdatePortalNotificationPreferencesCommand(phone, body)));
    }

    [HttpGet("payment-gateway")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPaymentGatewayInfo()
        => Ok(await Mediator.Send(new GetPortalPaymentGatewayInfoQuery()));
}
