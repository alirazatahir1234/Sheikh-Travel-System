using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.SmartOps;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Services.SmartOperations;

public class SmartAlertService(
    ISmartAlertRepository repository,
    INotificationRealtimePublisher realtime) : ISmartAlertService
{
    public async Task<SmartAlertSummaryDto> GetActiveAlertsAsync(
        int tenantId,
        SmartAlertRuleType? ruleType = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await repository.GetActiveAsync(tenantId, ruleType, limit, cancellationToken);
        var newCount = items.Count(a => string.Equals(a.Status, "New", StringComparison.OrdinalIgnoreCase));
        return new SmartAlertSummaryDto(newCount, items.Count, items);
    }

    public Task<int> GetNewCountAsync(int tenantId, CancellationToken cancellationToken = default)
        => repository.CountNewAsync(tenantId, cancellationToken);

    public async Task<SmartAlertDto?> AcknowledgeAlertAsync(
        int tenantId, long id, string actor, CancellationToken cancellationToken = default)
    {
        if (!await repository.AcknowledgeAsync(tenantId, id, actor, cancellationToken))
            return null;
        return await repository.GetByIdAsync(tenantId, id, cancellationToken);
    }

    public async Task<SmartAlertDto?> ResolveAlertAsync(
        int tenantId, long id, string actor, CancellationToken cancellationToken = default)
    {
        if (!await repository.ResolveAsync(tenantId, id, actor, cancellationToken))
            return null;
        return await repository.GetByIdAsync(tenantId, id, cancellationToken);
    }

    public async Task<SmartAlertDto?> DismissAlertAsync(
        int tenantId, long id, string actor, CancellationToken cancellationToken = default)
    {
        if (!await repository.DismissAsync(tenantId, id, actor, cancellationToken))
            return null;
        return await repository.GetByIdAsync(tenantId, id, cancellationToken);
    }

    public async Task<(SmartAlertDto Alert, bool Created)> RaiseAsync(
        CreateSmartAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await repository.TryInsertAsync(request, cancellationToken);
        if (result.Created)
        {
            await realtime.PublishToDispatchersAsync(new
            {
                kind = "smart_alert",
                tenantId = result.Alert.TenantId,
                id = result.Alert.Id,
                ruleType = result.Alert.RuleType,
                severity = result.Alert.Severity,
                message = result.Alert.Message,
                status = result.Alert.Status,
                relatedEntityType = result.Alert.RelatedEntityType,
                relatedEntityId = result.Alert.RelatedEntityId,
                createdAt = result.Alert.CreatedAt,
                title = $"Ops: {result.Alert.RuleType}"
            }, cancellationToken);
        }

        return result;
    }
}
