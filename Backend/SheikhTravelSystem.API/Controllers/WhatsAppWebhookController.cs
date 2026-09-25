using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Infrastructure.Services.WhatsApp;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/whatsapp/webhook")]
[EnableRateLimiting("public")]
public class WhatsAppWebhookController(
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppWebhookController> logger) : BaseApiController
{
    /// <summary>Meta webhook verification challenge.</summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (WhatsAppWebhookVerifier.TryGetChallenge(
                mode, verifyToken, challenge, options.Value.EffectiveVerifyToken, out var challengeResponse))
        {
            return Content(challengeResponse!, "text/plain");
        }

        // Never log the verify token.
        logger.LogWarning("WhatsApp webhook verify rejected. Mode={Mode}", mode);
        return Forbid();
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();

        if (!WhatsAppWebhookSignature.IsValid(options.Value.AppSecret, payload, signature))
        {
            logger.LogWarning("WhatsApp webhook signature invalid.");
            return Unauthorized();
        }

        // Always ACK 200 after a valid signature so Meta does not retry on business failures.
        var result = await Mediator.Send(new IngestWhatsAppWebhookCommand(payload));
        if (!result.Success)
            logger.LogWarning("WhatsApp webhook ingest reported failure: {Message}", result.Message);

        return Ok();
    }
}
