using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public record NotificationDecisionRequest(
    string EventType,
    string Title,
    string Message,
    NotificationType Type,
    int? ReferenceId = null,
    int? AlertEventId = null,
    int? TenantId = null,
    int? TargetUserId = null,
    IReadOnlyList<string>? RequestedChannels = null,
    int? SuggestedPriority = null,
    bool Broadcast = false,
    bool RequireEscalation = false);

public record NotificationDecisionResult(
    bool ShouldNotify,
    string Decision,
    string Reason,
    int Priority,
    IReadOnlyList<string> Channels,
    string? CooldownKey = null);

public interface INotificationDecisionEngine
{
    Task<NotificationDecisionResult> EvaluateAsync(
        NotificationDecisionRequest request,
        CancellationToken cancellationToken = default);

    Task<int> DispatchIfAllowedAsync(
        NotificationDecisionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IUserPresenceService
{
    Task SetBrowserOnlineAsync(int userId, bool online, CancellationToken cancellationToken = default);
    Task SetMobileHeartbeatAsync(int userId, CancellationToken cancellationToken = default);
    Task MarkLoginAsync(int userId, CancellationToken cancellationToken = default);
    Task MarkReadAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserPresenceSnapshot?> GetAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SelectChannelsAsync(
        int? userId,
        int priority,
        IReadOnlyList<string> candidateChannels,
        CancellationToken cancellationToken = default);
}

public record UserPresenceSnapshot(
    int UserId,
    bool BrowserOnline,
    bool MobileOnline,
    DateTime? LastBrowserAt,
    DateTime? LastMobileAt,
    DateTime? LastLoginAt,
    DateTime? LastReadAt);

public interface IDeviceTokenService
{
    Task RegisterAsync(int userId, string token, string platform, string appName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetActiveTokensAsync(int userId, int? tenantId = null, CancellationToken cancellationToken = default);
}

public interface IAlertNotificationAudit
{
    Task LogAsync(int alertEventId, string channel, string? recipient, string status, string? error = null, CancellationToken cancellationToken = default);
    Task<bool> IsAlertTypeEnabledAsync(int userId, string alertType, string channel, CancellationToken cancellationToken = default);
}

public interface IFleetHealthService
{
    Task<FleetHealthDto> ComputeAsync(int tenantId, CancellationToken cancellationToken = default);
}

public record FleetHealthDto(
    decimal HealthPercent,
    decimal GpsOnlineRate,
    decimal MaintenanceScore,
    decimal ComplianceScore,
    decimal DriverScore,
    int CriticalAlerts,
    string Summary);

public interface IAiDigestService
{
    Task GenerateMorningDigestAsync(int tenantId, CancellationToken cancellationToken = default);
}

public interface IAiRecommendationService
{
    Task RefreshAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiRecommendationDto>> GetActiveAsync(int tenantId, CancellationToken cancellationToken = default);
}

public record AiRecommendationDto(
    int Id,
    string EntityType,
    int EntityId,
    string Category,
    string Severity,
    string Title,
    string Action,
    string Source,
    decimal? Score,
    DateTime CreatedAt);

public interface IAiPredictionService
{
    Task CaptureFeaturesAsync(int tenantId, CancellationToken cancellationToken = default);
    Task RunHeuristicPredictionsAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiPredictionDto>> GetPredictionsAsync(int tenantId, string? entityType = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiDatasetStatusDto>> GetDatasetStatusAsync(int tenantId, CancellationToken cancellationToken = default);
}

public record AiDatasetStatusDto(
    string Name,
    long RowCount,
    DateTime? LastCapturedAt,
    string Freshness);

public record AiPredictionDto(
    int Id,
    string EntityType,
    int EntityId,
    string PredictionType,
    decimal Probability,
    int? ExpectedDays,
    string? Label,
    string ModelVersion,
    DateTime CreatedAt);

public interface IAiCopilotService
{
    Task<AiCopilotResponse> AskAsync(int tenantId, int userId, string question, CancellationToken cancellationToken = default);
}

public record AiCopilotResponse(string Answer, string Mode, IReadOnlyList<string> ToolsUsed, bool UsedLlm);

public interface IEscalationService
{
    Task StartAsync(
        string eventType,
        int? referenceId,
        int? alertEventId = null,
        int? notificationId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EscalationRuleDto>> GetRulesAsync(int? tenantId = null, CancellationToken cancellationToken = default);
    Task<EscalationRuleDto> UpsertRuleAsync(EscalationRuleDto rule, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EscalationPendingDto>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(int stateId, CancellationToken cancellationToken = default);
}

public record EscalationRuleDto(
    int Id,
    int? TenantId,
    string EventType,
    int LevelOrder,
    string TargetRole,
    int TimeoutMinutes,
    string Channel,
    bool IsActive);

public record EscalationPendingDto(
    int Id,
    string EventType,
    int CurrentLevel,
    int? ReferenceId,
    int? AlertEventId,
    DateTime? NextEscalateAt,
    string Status,
    DateTime CreatedAt);

public interface IAiManagementService
{
    Task<AiProviderConfigDto> GetConfigAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<AiProviderConfigDto> UpsertConfigAsync(int tenantId, AiProviderConfigDto config, CancellationToken cancellationToken = default);
    Task RecordUsageAsync(int tenantId, string feature, string provider, int tokens, decimal? costUsd = null, CancellationToken cancellationToken = default);
    Task RecordLearningAsync(int tenantId, int userId, string eventType, string action, CancellationToken cancellationToken = default);
}

public record AiProviderConfigDto(
    string Provider = "None",
    bool IsEnabled = false,
    bool CopilotEnabled = false,
    bool DecisionEngineEnabled = true,
    bool DigestEnabled = true,
    bool PredictionsEnabled = true,
    decimal? MonthlyBudgetUsd = null,
    int? SoftTokenLimit = null,
    string? ApiEndpoint = null,
    string? ModelName = null);
