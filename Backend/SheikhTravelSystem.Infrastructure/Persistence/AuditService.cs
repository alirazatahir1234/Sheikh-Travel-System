using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Thin wrapper over <see cref="IAuditEngine"/> for legacy callers.
/// Dual-write and Stage 13 gates live in the engine.
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
                Message: $"{action} {entityName}"), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditService.LogAsync failed for {Action} {Entity}", action, entityName);
        }
    }
}
