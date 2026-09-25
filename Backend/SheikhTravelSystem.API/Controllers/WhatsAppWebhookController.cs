using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.WhatsApp;
using SheikhTravelSystem.Infrastructure.Services.WhatsApp;

namespace SheikhTravelSystem.API.Controllers;

[ApiController]
[Route("api/whatsapp/webhook")]
[EnableRateLimiting("public")]
public class WhatsAppWebhookController(
    IOptions<WhatsAppOptions> options,
    IWhatsAppInboxExtended repository,
    IWhatsAppWorkQueue workQueue,
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

        logger.LogWarning("WhatsApp webhook verify rejected. Mode={Mode}", mode);
        return Forbid();
    }

    /// <summary>
    /// Fast path: verify HMAC → insert log → enqueue → 200 (processing is async).
    /// Invalid signature → 401 + log, never enqueue.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        var signatureValid = WhatsAppWebhookSignature.IsValid(options.Value.AppSecret, payload, signature);

        if (!signatureValid)
        {
            logger.LogWarning("WhatsApp webhook signature invalid.");
            try
            {
                await repository.InsertWebhookLogAsync(
                    signatureValid: false,
                    payload: payload.Length > 8000 ? payload[..8000] : payload,
                    processingStatus: "Rejected",
                    error: "Invalid signature",
                    ct: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist invalid webhook log");
            }

            return Unauthorized();
        }

        long logId;
        try
        {
            logId = await repository.InsertWebhookLogAsync(
                signatureValid: true,
                payload: payload,
                processingStatus: "Received",
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            // Still ACK so Meta does not storm-retry on transient DB issues; log for ops.
            logger.LogError(ex, "WhatsApp webhook log insert failed; acknowledging without enqueue");
            return Ok();
        }

        await workQueue.EnqueueInboundAsync(
            new WhatsAppInboundWorkItem(logId, payload, SignatureValid: true),
            cancellationToken);

        return Ok();
    }
}
