using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that records AuditEvents after auditable commands.
/// Soft-logs failures as Error events when Stage 13 level allows.
/// </summary>
public class AuditLoggingBehavior<TRequest, TResponse>(
    IAuditEngine auditEngine,
    ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IAuditableCommand auditable)
            return await next();

        try
        {
            var response = await next();

            int? entityId = auditable.AuditEntityId;
            var success = AuditEngineRules.ResolveCommandSuccess(response);

            if (entityId == null && response is not null && success)
            {
                var responseType = response.GetType();
                if (responseType.IsGenericType
                    && responseType.GetGenericTypeDefinition().FullName?.StartsWith(
                        "SheikhTravelSystem.Application.Common.ApiResponse") == true
                    && responseType.GenericTypeArguments.Length == 1
                    && responseType.GenericTypeArguments[0] == typeof(int))
                {
                    entityId = responseType.GetProperty("Data")?.GetValue(response) as int?;
                }
            }

            var eventKey = AuditEventRegistrySeed.ResolveEventKey(
                auditable.AuditEntityName, auditable.AuditAction);

            await auditEngine.RecordAsync(new AuditEventWrite(
                EventKey: eventKey,
                EntityType: auditable.AuditEntityName,
                EntityId: entityId,
                Action: auditable.AuditAction,
                Success: success,
                Message: $"{auditable.AuditAction} {auditable.AuditEntityName}"), cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            try
            {
                await auditEngine.RecordAsync(new AuditEventWrite(
                    EventKey: AuditEventKeys.GenericError,
                    EntityType: auditable.AuditEntityName,
                    EntityId: auditable.AuditEntityId,
                    Action: auditable.AuditAction,
                    Success: false,
                    Message: Truncate(ex.Message, 500)), cancellationToken);
            }
            catch (Exception auditEx)
            {
                logger.LogDebug(auditEx, "Failed to record audit error event");
            }

            throw;
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : value[..max];
    }
}
