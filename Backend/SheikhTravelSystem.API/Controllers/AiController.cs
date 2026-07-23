using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.API.Controllers;

[Authorize]
[RequirePermission(AiPermissions.View)]
[Route("api/ai")]
public class AiController(
    IFleetHealthService fleetHealth,
    IAiDigestService digests,
    IAiRecommendationService recommendations,
    IAiPredictionService predictions,
    IAiCopilotService copilot,
    IAiChatGateway chatGateway,
    IAiToolEngine toolEngine,
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

    /// <summary>Phase 1 AI Gateway chat (Ollama/Mistral with session memory; rules fallback).</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message) && !request.ConfirmWrite)
            return BadRequest(new { message = "Message is required." });
        if (request.ConfirmWrite && request.SessionId is null)
            return BadRequest(new { message = "SessionId is required to confirm a pending action." });

        var result = await chatGateway.ChatAsync(
            TenantId,
            UserId,
            new AiChatTurnRequest(
                request.Message ?? string.Empty,
                request.SessionId,
                request.Title,
                request.ConfirmWrite),
            ct);
        return Ok(result);
    }

    [HttpGet("chat/sessions/{sessionId:guid}/pending")]
    public async Task<IActionResult> GetPendingAction(Guid sessionId, CancellationToken ct)
        => Ok(await chatGateway.GetPendingActionAsync(TenantId, UserId, sessionId, ct));

    [HttpGet("chat/sessions")]
    public async Task<IActionResult> ListChatSessions(CancellationToken ct)
        => Ok(await chatGateway.ListSessionsAsync(TenantId, UserId, ct));

    [HttpGet("chat/sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> GetChatMessages(Guid sessionId, CancellationToken ct)
        => Ok(await chatGateway.GetMessagesAsync(TenantId, UserId, sessionId, ct));

    [HttpGet("chat/provider-health")]
    public async Task<IActionResult> GetChatProviderHealth(CancellationToken ct)
        => Ok(await chatGateway.GetProviderHealthAsync(TenantId, ct));

    [HttpGet("chat/tools")]
    public IActionResult ListTools()
        => Ok(toolEngine.ListTools(includeWriteTools: true));

    [RequirePermission(AiPermissions.Manage)]
    [HttpGet("management/config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
        => Ok(await management.GetConfigAsync(TenantId, ct));

    [RequirePermission(AiPermissions.Manage)]
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

    [RequirePermission(AiPermissions.Manage)]
    [HttpGet("escalation/rules")]
    public async Task<IActionResult> GetEscalationRules(CancellationToken ct)
        => Ok(await escalation.GetRulesAsync(TenantId, ct));

    [RequirePermission(AiPermissions.Manage)]
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

    [RequirePermission(AiPermissions.Manage)]
    [HttpGet("datasets")]
    public async Task<IActionResult> GetDatasets(CancellationToken ct)
    {
        await predictions.CaptureFeaturesAsync(TenantId, ct);
        // Freshness / counts for AI feature store admin surface
        return Ok(await predictions.GetDatasetStatusAsync(TenantId, ct));
    }
}

public record AiAskRequest(string Question);
public record AiChatRequest(
    string Message,
    Guid? SessionId = null,
    string? Title = null,
    bool ConfirmWrite = false);
public record AiLearningRequest(string EventType, string Action);
public record RegisterDeviceTokenRequest(string Token, string? Platform = null, string? AppName = null);
