using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[Route("api/ai")]
public class AiController(
    IFleetHealthService fleetHealth,
    IAiDigestService digests,
    IAiRecommendationService recommendations,
    IAiPredictionService predictions,
    IAiCopilotService copilot,
    IAiManagementService management,
    INotificationDecisionEngine decisionEngine,
    IDeviceTokenService deviceTokens,
    IUserPresenceService presence,
    IEscalationService escalation,
    ITenantContext tenantContext,
    ICurrentUserService currentUser) : BaseApiController
{
    private int TenantId => tenantContext.TenantId ?? 1;
    private int UserId => currentUser.UserId ?? throw new UnauthorizedAccessException();

    [HttpGet("health")]
    public async Task<IActionResult> GetFleetHealth(CancellationToken ct)
        => Ok(await fleetHealth.ComputeAsync(TenantId, ct));

    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations(CancellationToken ct)
    {
        await recommendations.RefreshAsync(TenantId, ct);
        return Ok(await recommendations.GetActiveAsync(TenantId, ct));
    }

    [HttpPost("recommendations/refresh")]
    public async Task<IActionResult> RefreshRecommendations(CancellationToken ct)
    {
        await recommendations.RefreshAsync(TenantId, ct);
        return Ok(new { refreshed = true });
    }

    [HttpGet("predictions")]
    public async Task<IActionResult> GetPredictions([FromQuery] string? entityType, CancellationToken ct)
        => Ok(await predictions.GetPredictionsAsync(TenantId, entityType, ct));

    [HttpPost("predictions/run")]
    public async Task<IActionResult> RunPredictions(CancellationToken ct)
    {
        await predictions.CaptureFeaturesAsync(TenantId, ct);
        await predictions.RunHeuristicPredictionsAsync(TenantId, ct);
        return Ok(await predictions.GetPredictionsAsync(TenantId, null, ct));
    }

    [HttpPost("digest/morning")]
    public async Task<IActionResult> GenerateDigest(CancellationToken ct)
    {
        await digests.GenerateMorningDigestAsync(TenantId, ct);
        return Ok(new { generated = true });
    }

    [HttpPost("copilot/ask")]
    public async Task<IActionResult> Ask([FromBody] AiAskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Question is required." });
        return Ok(await copilot.AskAsync(TenantId, UserId, request.Question, ct));
    }

    [HttpGet("management/config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
        => Ok(await management.GetConfigAsync(TenantId, ct));

    [HttpPut("management/config")]
    public async Task<IActionResult> UpsertConfig([FromBody] AiProviderConfigDto config, CancellationToken ct)
        => Ok(await management.UpsertConfigAsync(TenantId, config, ct));

    [HttpPost("learning")]
    public async Task<IActionResult> RecordLearning([FromBody] AiLearningRequest request, CancellationToken ct)
    {
        await management.RecordLearningAsync(TenantId, UserId, request.EventType, request.Action, ct);
        return Ok(new { recorded = true });
    }

    [HttpPost("device-tokens")]
    public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        await deviceTokens.RegisterAsync(
            UserId,
            request.Token,
            request.Platform ?? "android",
            request.AppName ?? "driver",
            ct);
        await presence.SetMobileHeartbeatAsync(UserId, ct);
        return Ok(new { registered = true });
    }

    [HttpPost("presence/mobile-heartbeat")]
    public async Task<IActionResult> MobileHeartbeat(CancellationToken ct)
    {
        await presence.SetMobileHeartbeatAsync(UserId, ct);
        return Ok(new { ok = true });
    }

    [HttpPost("decision/evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] NotificationDecisionRequest request, CancellationToken ct)
        => Ok(await decisionEngine.EvaluateAsync(request, ct));

    [HttpGet("escalation/rules")]
    public async Task<IActionResult> GetEscalationRules(CancellationToken ct)
        => Ok(await escalation.GetRulesAsync(TenantId, ct));

    [HttpPut("escalation/rules")]
    public async Task<IActionResult> UpsertEscalationRule([FromBody] EscalationRuleDto rule, CancellationToken ct)
        => Ok(await escalation.UpsertRuleAsync(rule with { TenantId = rule.TenantId ?? TenantId }, ct));

    [HttpGet("escalation/pending")]
    public async Task<IActionResult> GetPendingEscalations(CancellationToken ct)
        => Ok(await escalation.GetPendingAsync(ct));

    [HttpPost("escalation/{id:int}/ack")]
    public async Task<IActionResult> AckEscalation(int id, CancellationToken ct)
    {
        await escalation.AcknowledgeAsync(id, ct);
        return Ok(new { acknowledged = true });
    }

    [HttpGet("datasets")]
    public async Task<IActionResult> GetDatasets(CancellationToken ct)
    {
        await predictions.CaptureFeaturesAsync(TenantId, ct);
        // Freshness / counts for AI feature store admin surface
        return Ok(await predictions.GetDatasetStatusAsync(TenantId, ct));
    }
}

public record AiAskRequest(string Question);
public record AiLearningRequest(string EventType, string Action);
public record RegisterDeviceTokenRequest(string Token, string? Platform = null, string? AppName = null);
