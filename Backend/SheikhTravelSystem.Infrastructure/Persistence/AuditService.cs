using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Thin wrapper over <see cref="IAuditEngine"/> for legacy callers.
/// Fail-open + loud. Prefer explicit <see cref="IAuditEngine"/> for security events.
/// </summary>
public class AuditService(
    IAuditEngine auditEngine,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task LogAsync(string action, string entityName, int? entityId, CancellationToken cancellationToken = default)
    {
        try
        {
            var eventKey = AuditEventRegistrySeed.ResolveEventKey(entityName, action);
            await auditEngine.RecordAsync(new AuditEventWrite(
                EventKey: eventKey,
                EntityType: entityName,
                EntityId: entityId,
                Action: action,
                Success: true,
                Message: $"{action} {entityName}",
                FailPolicy: AuditFailPolicy.FailOpenLoud), cancellationToken);
        }
        catch (Exception ex)
        {
            AuditWriteMetrics.RecordFailure(
                AuditEventRegistrySeed.ResolveEventKey(entityName, action),
                "audit_service");
            logger.LogError(ex, "Fail-open audit write failed for {Action} {Entity}", action, entityName);
        }
    }
}
