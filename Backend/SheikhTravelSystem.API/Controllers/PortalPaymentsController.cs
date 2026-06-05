using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.CustomerPortal;
using SheikhTravelSystem.Application.Features.CustomerPortal.Commands;
using SheikhTravelSystem.Infrastructure.Services.Payments;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/customer-portal/payments")]
public class PortalPaymentsController : BaseApiController
{
    [Authorize(Roles = "PortalCustomer")]
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] PortalPaymentCheckoutRequest body)
    {
        var phone = PortalUserContext.GetPhone(User);
        var customerId = PortalUserContext.GetCustomerId(User);
        return Ok(await Mediator.Send(new CreatePortalPaymentCheckoutCommand(body.BookingId, body.Amount, phone ?? "", customerId)));
    }

    [AllowAnonymous]
    [HttpPost("webhook/stripe")]
    public Task<IActionResult> StripeWebhook([FromServices] IEnumerable<IPaymentGatewayProvider> gateways)
        => HandleGatewayWebhook(gateways, "Stripe");

    [AllowAnonymous]
    [HttpPost("webhook/jazzcash")]
    public Task<IActionResult> JazzCashWebhook([FromServices] IEnumerable<IPaymentGatewayProvider> gateways)
        => HandleGatewayWebhook(gateways, "JazzCash");

    [AllowAnonymous]
    [HttpPost("webhook/easypaisa")]
    public Task<IActionResult> EasyPaisaWebhook([FromServices] IEnumerable<IPaymentGatewayProvider> gateways)
        => HandleGatewayWebhook(gateways, "EasyPaisa");

    private async Task<IActionResult> HandleGatewayWebhook(
        IEnumerable<IPaymentGatewayProvider> gateways,
        string providerName)
    {
        var gateway = gateways.FirstOrDefault(g =>
            string.Equals(g.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        if (gateway is null)
        {
            return BadRequest();
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault()
            ?? Request.Headers["X-Signature"].FirstOrDefault()
            ?? "";
        var ok = await gateway.HandleWebhookAsync(payload, signature);
        return ok ? Ok() : BadRequest();
    }
}

public record PortalPaymentCheckoutRequest(int BookingId, decimal Amount);
