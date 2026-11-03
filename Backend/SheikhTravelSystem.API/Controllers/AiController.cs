using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SheikhTravelSystem.API.Authorization;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.AI.Commands;
using SheikhTravelSystem.Application.Features.AI.Queries;
using SheikhTravelSystem.Infrastructure.Services.Ai;

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
    IAiAssistantGateway assistantGateway,
    ISheikhGoMcpBridge mcpBridge,
    IAiToolEngine toolEngine,
    IAiManagementService management,
    INotificationDecisionEngine decisionEngine,
    IDeviceTokenService deviceTokens,
    IUserPresenceService presence,
    IEscalationService escalation,
    ITenantContext tenantContext,
    ICurrentUserService currentUser,
    IMcpExecutionAmbient executionAmbient) : BaseApiController
{
    private static readonly TimeSpan ChatTurnBudget = TimeSpan.FromSeconds(75);

    private int UserId => currentUser.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>Fail closed — never fall back to tenant 1 when context is missing.</summary>
    private bool TryResolveTenantId(out int tenantId, out IActionResult? error)
    {
        if (tenantContext.TenantId is int tid && tid > 0)
        {
            tenantId = tid;
            error = null;
            return true;
        }

        tenantId = 0;
        error = Unauthorized(new { message = "Tenant context is required." });
        return false;
    }

    /// <summary>
    /// Platform SuperAdmin may narrow scope via body <c>targetTenantId</c> (defense in depth with X-Tenant-Id).
    /// Non–SuperAdmin forged <c>targetTenantId</c> that differs from JWT/middleware tenant is rejected (403).
    /// </summary>
    private bool TryResolveEffectiveTenantId(
        int? requestedTargetTenantId,
        out int tenantId,
        out IActionResult? error)
    {
        var resolved = AiEffectiveTenantResolver.Resolve(
            tenantContext.TenantId,
            currentUser.IsPlatformSuperAdmin,
            requestedTargetTenantId);

        switch (resolved.Status)
        {
            case AiEffectiveTenantResolver.ResolveStatus.Ok:
                tenantId = resolved.TenantId;
                error = null;
                return true;
            case AiEffectiveTenantResolver.ResolveStatus.ForgedTargetTenantRejected:
                tenantId = 0;
                error = StatusCode(StatusCodes.Status403Forbidden, new { message = resolved.Message });
                return false;
            default:
                tenantId = 0;
                error = Unauthorized(new { message = resolved.Message ?? "Tenant context is required." });
                return false;
        }
    }

    [RequirePermission(AiPermissions.View)]
    [HttpGet("health")]
    public async Task<IActionResult> GetFleetHealth(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        return Ok(await fleetHealth.ComputeAsync(tenantId, ct));
    }

    [RequirePermission(AiPermissions.ViewRecommendations)]
    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        var result = await Mediator.Send(new GetAiRecommendationsQuery(tenantId), ct);
        return FromAiResponse(result);
    }

    [RequirePermission(AiPermissions.RefreshRecommendations)]
    [HttpPost("recommendations/refresh")]
    public async Task<IActionResult> RefreshRecommendations(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        var result = await Mediator.Send(new RefreshAiRecommendationsCommand(tenantId), ct);
        if (!result.Success)
            return BadRequest(new { message = result.Message });
        return Ok(new { refreshed = true });
    }

    [RequirePermission(AiPermissions.ViewPredictions)]
    [HttpGet("predictions")]
    public async Task<IActionResult> GetPredictions([FromQuery] string? entityType, CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        var result = await Mediator.Send(new GetAiPredictionsQuery(tenantId, entityType), ct);
        return FromAiResponse(result);
    }

    [RequirePermission(AiPermissions.RunPredictions)]
    [HttpPost("predictions/run")]
    public async Task<IActionResult> RunPredictions(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        var result = await Mediator.Send(new RunAiPredictionsCommand(tenantId), ct);
        return FromAiResponse(result);
    }

    [RequirePermission(AiPermissions.Manage)]
    [HttpPost("digest/morning")]
    public async Task<IActionResult> GenerateDigest(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        await digests.GenerateMorningDigestAsync(tenantId, ct);
        return Ok(new { generated = true });
    }

    /// <summary>Premium SheikhGo AI Assistant — requires AI module + Ai.Chat.</summary>
    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpPost("copilot/ask")]
    public async Task<IActionResult> Ask([FromBody] AiAskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Question is required." });
        if (!TryResolveEffectiveTenantId(request.TargetTenantId, out var tenantId, out var error))
            return error!;
        return Ok(await copilot.AskAsync(tenantId, UserId, request.Question, ct));
    }

    /// <summary>Phase 2 AI Assistant — allowlisted read-only reporting tools.</summary>
    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpPost("assistant/query")]
    public async Task<IActionResult> AssistantQuery([FromBody] AiAskRequest request, CancellationToken ct)
    {
        if (!TryResolveEffectiveTenantId(request.TargetTenantId, out var tenantId, out var error))
            return error!;

        var vehicleId = request.VehicleId is int vid && vid > 0 ? vid : (int?)null;
        var sessionId = request.SessionId is Guid sid && sid != Guid.Empty ? sid : (Guid?)null;
        var result = await Mediator.Send(
            new QueryAiAssistantCommand(
                tenantId,
                UserId,
                request.Question ?? string.Empty,
                currentUser.IsPlatformSuperAdmin,
                vehicleId,
                sessionId),
            ct);
        return FromAiResponse(result);
    }

    /// <summary>
    /// Assistant query with optional file attachments (text/CSV/JSON/Markdown/images/PDF).
    /// Text is inlined into the question; images/PDF are noted for follow-up (no binary stored in chat).
    /// </summary>
    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpPost("assistant/query-with-files")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> AssistantQueryWithFiles(
        [FromForm] string? question,
        [FromForm] Guid? sessionId,
        [FromForm] int? targetTenantId,
        [FromForm] int? vehicleId,
        [FromForm] List<IFormFile>? files,
        CancellationToken ct)
    {
        if (!TryResolveEffectiveTenantId(targetTenantId, out var tenantId, out var error))
            return error!;

        var (enriched, attachWarnings) = await AiChatAttachmentHelper.EnrichQuestionAsync(
            question ?? string.Empty, files, ct);

        if (string.IsNullOrWhiteSpace(enriched))
            return BadRequest(new { message = "Question or supported attachment is required." });

        var vid = vehicleId is int v && v > 0 ? v : (int?)null;
        var sid = sessionId is Guid s && s != Guid.Empty ? s : (Guid?)null;
        var result = await Mediator.Send(
            new QueryAiAssistantCommand(
                tenantId,
                UserId,
                enriched,
                currentUser.IsPlatformSuperAdmin,
                vid,
                sid),
            ct);

        // Surface attach warnings without failing the turn.
        if (result.Success && result.Data is not null && attachWarnings.Count > 0)
        {
            var data = result.Data;
            var mergedWarnings = (data.Warnings ?? Array.Empty<string>())
                .Concat(attachWarnings)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            result = ApiResponse<AiAssistantQueryResponse>
                .SuccessResponse(data with { Warnings = mergedWarnings });
        }

        return FromAiResponse(result);
    }

    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("assistant/tools")]
    public IActionResult AssistantTools()
        => Ok(assistantGateway.ListAllowlistedTools());

    /// <summary>
    /// Allowlisted SheikhGo MCP tools for SheikhGo AI Assistant (ADR-AI-001). Architecture/write tools are never listed.
    /// </summary>
    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("mcp/tools")]
    public IActionResult McpAllowlistedTools()
        => Ok(new
        {
            enabled = mcpBridge.IsEnabled,
            tools = mcpBridge.ListAllowlistedToolNames()
        });

    /// <summary>AI Gateway chat. CQRS: SendAiChatCommand → IAiChatGateway. Optional MCP tool-calling when Ai:McpBridge:Enabled.</summary>
    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken ct)
    {
        if (!TryResolveEffectiveTenantId(request.TargetTenantId, out var tenantId, out var error))
            return error!;

        // Hard turn budget: provider retries × tool rounds can otherwise exceed every
        // client/proxy timeout and leave the caller waiting with no answer.
        using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        turnCts.CancelAfter(ChatTurnBudget);
        try
        {
            var result = await Mediator.Send(
                new SendAiChatCommand(
                    tenantId,
                    UserId,
                    request.Message ?? string.Empty,
                    request.SessionId,
                    request.Title,
                    request.ConfirmWrite),
                turnCts.Token);
            return FromAiResponse(result);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                message = "The assistant took too long to answer. Try a shorter or more specific question."
            });
        }
    }

    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("chat/sessions/{sessionId:guid}/pending")]
    public async Task<IActionResult> GetPendingAction(Guid sessionId, CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        return Ok(await chatGateway.GetPendingActionAsync(tenantId, UserId, sessionId, ct));
    }

    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("chat/sessions")]
    public async Task<IActionResult> ListChatSessions(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        return Ok(await chatGateway.ListSessionsAsync(tenantId, UserId, ct));
    }

    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("chat/sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> GetChatMessages(Guid sessionId, CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        return Ok(await chatGateway.GetMessagesAsync(tenantId, UserId, sessionId, ct));
    }

    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.ViewProviderHealth)]
    [HttpGet("chat/provider-health")]
    public async Task<IActionResult> GetChatProviderHealth(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        var result = await Mediator.Send(new GetAiProviderHealthQuery(tenantId), ct);
        return FromAiResponse(result);
    }

    [RequireModule(TenantModuleCatalog.AiAssistantModuleCode)]
    [RequirePermission(AiPermissions.Chat)]
    [HttpGet("chat/tools")]
    public IActionResult ListTools()
        => Ok(toolEngine.ListTools(includeWriteTools: true));

    [RequirePermission(AiPermissions.ManageProviders)]
    [HttpGet("management/config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        return Ok(await management.GetConfigAsync(tenantId, ct));
    }

    [RequirePermission(AiPermissions.ManageProviders)]
    [HttpPut("management/config")]
    public async Task<IActionResult> UpsertConfig([FromBody] AiProviderConfigDto config, CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        return Ok(await management.UpsertConfigAsync(tenantId, config, ct));
    }

    [RequirePermission(AiPermissions.Chat)]
    [HttpPost("learning")]
    public async Task<IActionResult> RecordLearning([FromBody] AiLearningRequest request, CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        await management.RecordLearningAsync(tenantId, UserId, request.EventType, request.Action, ct);
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
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        return Ok(await escalation.GetRulesAsync(tenantId, ct));
    }

    [RequirePermission(AiPermissions.Manage)]
    [HttpPut("escalation/rules")]
    public async Task<IActionResult> UpsertEscalationRule([FromBody] EscalationRuleDto rule, CancellationToken ct)
    {
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        return Ok(await escalation.UpsertRuleAsync(rule with { TenantId = rule.TenantId ?? tenantId }, ct));
    }

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
        if (!TryResolveTenantId(out var tenantId, out var error))
            return error!;
        await predictions.CaptureFeaturesAsync(tenantId, ct);
        return Ok(await predictions.GetDatasetStatusAsync(tenantId, ct));
    }

    /// <summary>Preserve pre-CQRS AI response shape (raw payload, not ApiResponse envelope).</summary>
    private IActionResult FromAiResponse<T>(ApiResponse<T> response)
    {
        StampCorrelationHeader();
        if (!response.Success)
            return BadRequest(new { message = response.Message, errors = response.Errors });
        return Ok(response.Data);
    }

    /// <summary>Phase 8 — expose turn correlation id without leaking payloads.</summary>
    private void StampCorrelationHeader()
    {
        var id = executionAmbient.CorrelationId;
        if (string.IsNullOrWhiteSpace(id))
            return;
        Response.Headers["X-Correlation-Id"] = id;
    }
}

public record AiAskRequest(
    string Question,
    int? TargetTenantId = null,
    int? VehicleId = null,
    Guid? SessionId = null);
public record AiChatRequest(
    string Message,
    Guid? SessionId = null,
    string? Title = null,
    bool ConfirmWrite = false,
    int? TargetTenantId = null,
    int? VehicleId = null);
public record AiLearningRequest(string EventType, string Action);
public record RegisterDeviceTokenRequest(string Token, string? Platform = null, string? AppName = null);
