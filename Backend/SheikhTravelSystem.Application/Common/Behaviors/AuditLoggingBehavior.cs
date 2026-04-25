using MediatR;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that writes an AuditLogs entry after every
/// command that implements <see cref="IAuditableCommand"/>.
/// Runs after the handler succeeds — failures are not logged.
/// </summary>
public class AuditLoggingBehavior<TRequest, TResponse>(IAuditService auditService)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is not IAuditableCommand auditable)
            return response;

        // For Create commands AuditEntityId is null — try to extract the new ID
        // from ApiResponse<int>.Data returned by the handler.
        int? entityId = auditable.AuditEntityId;
        if (entityId == null && response is not null)
        {
            var responseType = response.GetType();
            if (responseType.IsGenericType
                && responseType.GetGenericTypeDefinition().FullName?.StartsWith("SheikhTravelSystem.Application.Common.ApiResponse") == true
                && responseType.GenericTypeArguments.Length == 1
                && responseType.GenericTypeArguments[0] == typeof(int))
            {
                var success = responseType.GetProperty("Success")?.GetValue(response) as bool?;
                if (success == true)
                    entityId = responseType.GetProperty("Data")?.GetValue(response) as int?;
            }
        }

        await auditService.LogAsync(
            auditable.AuditAction,
            auditable.AuditEntityName,
            entityId,
            cancellationToken);

        return response;
    }
}
