using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.AI.Commands;
using SheikhTravelSystem.Application.Features.AI.Queries;

namespace SheikhTravelSystem.API.Controllers;

/// <summary>
/// Thin AI Operations API. Orchestration lives in Features/AI (CQRS);
/// providers remain in Infrastructure/Services/Ai.
/// </summary>
[Authorize]
[Route("api/ai")]
public class AiController(
    IFleetHealthService fleetHealth,
    IAiDigestService digests,
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

    [RequirePermission(AiPermissions.View)]
    [HttpGet("health")]
    public async Task<IActionResult> GetFleetHealth(CancellationToken ct)
        => Ok(await fleetHealth.ComputeAsync(TenantId, ct));

    [RequirePermission(AiPermissions.ViewRecommendations)]
    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetAiRecommendationsQuery(TenantId), ct);
        return FromAiResponse(result);
    }

    [RequirePermission(AiPermissions.RefreshRecommendations)]
    [HttpPost("recommendations/refresh")]
    public async Task<IActionResult> RefreshRecommendations(CancellationToken ct)
    {
        var result = await Mediator.Send(new RefreshAiRecommendationsCommand(TenantId), ct);
        if (!result.Success)
            return BadRequest(new { message = result.Message });
        return Ok(new { refreshed = true });
    }

    [RequirePermission(AiPermissions.ViewPredictions)]
    [HttpGet("predictions")]
    public async Task<IActionResult> GetPredictions([FromQuery] string? entityType, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetAiPredictionsQuery(TenantId, entityType), ct);
        return FromAiResponse(result);
    }

    [RequirePermission(AiPermissions.RunPredictions)]
    [HttpPost("predictions/run")]
    public async Task<IActionResult> RunPredictions(CancellationToken ct)
    {
        var result = await Mediator.Send(new RunAiPredictionsCommand(TenantId), ct);
        return FromAiResponse(result);
    }

    [RequirePermission(AiPermissions.Manage)]
    [HttpPost("digest/morning")]
    public async Task<IActionResult> GenerateDigest(CancellationToken ct)
    {
        await digests.GenerateMorningDigestAsync(TenantId, ct);
        return Ok(new { generated = true });
    }

    [RequirePermission(AiPermissions.Chat)]
    [HttpPost("copilot/ask")]
    public async Task<IActionResult> Ask([FromBody] AiAskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Question is required." });
        return Ok(await copilot.AskAsync(TenantId, UserId, request.Question, ct));
    }

    /// <summary>AI Gateway chat. CQRS: SendAiChatCommand → IAiChatGateway.</summary>
    [RequirePermission(AiPermissions.Chat)]
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken ct)
    {
        var result = await Mediator.Send(
            new SendAiChatCommand(
                TenantId,
                UserId,
                request.Message ?? string.Empty,
                request.SessionId,
                request.Title,
                request.ConfirmWrite),
            ct);
        return FromAiResponse(result);
    }

    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("chat/sessions/{sessionId:guid}/pending")]
    public async Task<IActionResult> GetPendingAction(Guid sessionId, CancellationToken ct)
        => Ok(await chatGateway.GetPendingActionAsync(TenantId, UserId, sessionId, ct));

    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("chat/sessions")]
    public async Task<IActionResult> ListChatSessions(CancellationToken ct)
        => Ok(await chatGateway.ListSessionsAsync(TenantId, UserId, ct));

    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("chat/sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> GetChatMessages(Guid sessionId, CancellationToken ct)
        => Ok(await chatGateway.GetMessagesAsync(TenantId, UserId, sessionId, ct));

    [RequirePermission(AiPermissions.ViewProviderHealth)]
    [HttpGet("chat/provider-health")]
    public async Task<IActionResult> GetChatProviderHealth(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetAiProviderHealthQuery(TenantId), ct);
        return FromAiResponse(result);
    }

    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("chat/tools")]
    public IActionResult ListTools()
        => Ok(toolEngine.ListTools(includeWriteTools: true));

    [RequirePermission(AiPermissions.ManageProviders)]
    [HttpGet("management/config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
        => Ok(await management.GetConfigAsync(TenantId, ct));

    [RequirePermission(AiPermissions.ManageProviders)]
    [HttpPut("management/config")]
    public async Task<IActionResult> UpsertConfig([FromBody] AiProviderConfigDto config, CancellationToken ct)
        => Ok(await management.UpsertConfigAsync(TenantId, config, ct));

    [RequirePermission(AiPermissions.Chat)]
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

    [RequirePermission(AiPermissions.Manage)]
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

    [RequirePermission(AiPermissions.View)]
    [HttpGet("escalation/pending")]
    public async Task<IActionResult> GetPendingEscalations(CancellationToken ct)
        => Ok(await escalation.GetPendingAsync(ct));

    [RequirePermission(AiPermissions.View)]
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
        return Ok(await predictions.GetDatasetStatusAsync(TenantId, ct));
    }

    /// <summary>Preserve pre-CQRS AI response shape (raw payload, not ApiResponse envelope).</summary>
    private IActionResult FromAiResponse<T>(ApiResponse<T> response)
    {
        if (!response.Success)
            return BadRequest(new { message = response.Message, errors = response.Errors });
        return Ok(response.Data);
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
