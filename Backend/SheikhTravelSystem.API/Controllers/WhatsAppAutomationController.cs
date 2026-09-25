using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;
using SheikhTravelSystem.Application.Features.WhatsApp.Commands;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/whatsapp")]
public class WhatsAppAutomationController : BaseApiController
{
    [HttpGet("automation-rules")]
    [RequirePermission(WhatsAppPermissions.Manage)]
    public async Task<IActionResult> GetRules()
        => Ok(await Mediator.Send(new GetWhatsAppAutomationRulesQuery()));

    [HttpPut("automation-rules/{eventType}")]
    [RequirePermission(WhatsAppPermissions.Manage)]
    public async Task<IActionResult> UpsertRule(string eventType, [FromBody] UpsertAutomationRuleRequest body)
    {
        var result = await Mediator.Send(new UpsertWhatsAppAutomationRuleCommand(
            eventType,
            body.IsEnabled,
            body.AccountId,
            body.TemplateName,
            body.OffsetMinutes,
            body.NotifyBooker,
            body.IsUrgent));
        if (!result.Success && string.Equals(result.Code, "TEMPLATE_NOT_APPROVED", StringComparison.Ordinal))
            return Conflict(result);
        return Ok(result);
    }

    [HttpGet("automation-rules/{eventType}/preview")]
    [RequirePermission(WhatsAppPermissions.Manage)]
    public async Task<IActionResult> Preview(string eventType, [FromQuery] int bookingId)
        => Ok(await Mediator.Send(new PreviewWhatsAppAutomationQuery(eventType, bookingId)));

    [HttpPost("automation-events/{id:long}/resend")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> Resend(long id)
    {
        var result = await Mediator.Send(new ResendWhatsAppAutomationEventCommand(id));
        if (!result.Success)
            return BadRequest(result);
        return Accepted(result);
    }

    [HttpPost("consents")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> RecordConsent([FromBody] RecordConsentRequest body)
        => Ok(await Mediator.Send(new RecordWhatsAppConsentCommand(body.WaId ?? "", body.Note)));

    [HttpDelete("consents/{waId}")]
    [RequirePermission(WhatsAppPermissions.Reply)]
    public async Task<IActionResult> OptOut(string waId)
        => Ok(await Mediator.Send(new OptOutWhatsAppConsentCommand(waId)));
}

[Authorize]
[Route("api/bookings")]
public class BookingWhatsAppTimelineController : BaseApiController
{
    [HttpGet("{bookingId:int}/whatsapp-timeline")]
    [RequirePermission(WhatsAppPermissions.View)]
    public async Task<IActionResult> Timeline(int bookingId)
        => Ok(await Mediator.Send(new GetBookingWhatsAppTimelineQuery(bookingId)));
}

[AllowAnonymous]
[ApiController]
[Route("api/public/tracking")]
[EnableRateLimiting("public")]
public class PublicTrackingController(ILogger<PublicTrackingController> logger) : BaseApiController
{
    [HttpGet("{token}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(string token)
    {
        var result = await Mediator.Send(new GetPublicTrackingQuery(token));
        if (!result.Success || result.Data is null)
        {
            logger.LogDebug("Tracking token lookup failed (linkId omitted).");
            return NotFound(new { success = false, message = "This link is no longer active" });
        }

        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return Ok(result);
    }
}

[AllowAnonymous]
[ApiController]
[Route("t")]
public class PublicTrackingPageController : ControllerBase
{
    [HttpGet("{token}")]
    public IActionResult Page(string token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var html = System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "track", "index.html"))
            ? System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "track", "index.html"))
            : MinimalTrackingHtml(token);
        html = html.Replace("{{TOKEN}}", System.Net.WebUtility.HtmlEncode(token));
        return Content(html, "text/html");
    }

    private static string MinimalTrackingHtml(string token)
    {
        var safe = System.Text.Json.JsonSerializer.Serialize(token);
        return """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8"/>
          <meta name="viewport" content="width=device-width,initial-scale=1"/>
          <meta name="robots" content="noindex"/>
          <title>SheikhGo Live Track</title>
          <style>
            body{font-family:system-ui,sans-serif;margin:0;background:#0f172a;color:#f8fafc}
            .card{padding:1.25rem;max-width:480px;margin:0 auto}
            #map{height:55vh;background:#1e293b;border-radius:12px}
            .eta{font-size:1.4rem;font-weight:700;margin:.75rem 0}
            .meta{opacity:.85;font-size:.9rem;line-height:1.5}
            .err{padding:2rem;text-align:center}
            a.btn{display:inline-block;margin-top:1rem;padding:.75rem 1.25rem;background:#0f766e;color:#fff;
              text-decoration:none;border-radius:10px;font-weight:600}
          </style>
        </head>
        <body>
          <div class="card" id="app"><p>Loading…</p></div>
          <script>
            const token = __TOKEN__;
            const api = '/api/public/tracking/' + encodeURIComponent(token);
            async function refresh(){
              try{
                const r = await fetch(api,{cache:'no-store'});
                const j = await r.json();
                const d = j.data || j;
                if(!r.ok || !d || j.success===false){
                  document.getElementById('app').innerHTML='<div class="err"><h2>This link is no longer active</h2><a class="btn" href="tel:+92420000000">Call SheikhGo support</a></div>';
                  return;
                }
                const pos = d.position;
                const updated = pos ? new Date(pos.fixTime) : null;
                const ago = updated ? Math.round((Date.now()-updated.getTime())/1000) : null;
                document.getElementById('app').innerHTML = `
                  <h1 style="font-size:1.1rem;margin:0 0 .5rem">SheikhGo · ${d.state||''}</h1>
                  <div class="eta">${d.etaMinutes!=null?('ETA ~ '+d.etaMinutes+' min'): (d.state==='Arrived'?'Your driver is here':'')}</div>
                  <div id="map"></div>
                  <div class="meta">
                    <div><strong>${d.driverFirstName||'Driver'}</strong> · ${d.vehicle?.description||''} (${d.vehicle?.plate||''})</div>
                    <div>Pickup: ${d.pickup?.address||'—'}</div>
                    <div>${ago!=null?('Last updated '+ago+'s ago'):''}</div>
                  </div>
                  <a class="btn" href="tel:${(d.brand&&d.brand.supportPhone)||'+92420000000'}">Call SheikhGo support</a>`;
                if(pos){
                  document.getElementById('map').textContent = pos.lat.toFixed(5)+', '+pos.lng.toFixed(5)+' · '+(pos.speedKmh||0).toFixed(0)+' km/h';
                } else {
                  document.getElementById('map').textContent = 'Position available once driver is en route';
                }
              }catch(e){
                document.getElementById('app').innerHTML='<div class="err"><h2>This link is no longer active</h2></div>';
              }
            }
            refresh();
            setInterval(refresh, 5000);
          </script>
        </body>
        </html>
        """.Replace("__TOKEN__", safe);
    }
}

public record UpsertAutomationRuleRequest(
    bool IsEnabled,
    int? AccountId,
    string? TemplateName,
    int OffsetMinutes,
    bool NotifyBooker,
    bool IsUrgent);

public record RecordConsentRequest(string? WaId, string? Note);
