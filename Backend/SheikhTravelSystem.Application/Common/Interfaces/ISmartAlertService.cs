using SheikhTravelSystem.Application.Features.SmartOps;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces;

public interface ISmartAlertService
{
    Task<SmartAlertSummaryDto> GetActiveAlertsAsync(
        int tenantId,
        SmartAlertRuleType? ruleType = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<int> GetNewCountAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<SmartAlertDto?> AcknowledgeAlertAsync(
        int tenantId,
        long id,
        string actor,
        CancellationToken cancellationToken = default);

    Task<SmartAlertDto?> ResolveAlertAsync(
        int tenantId,
        long id,
        string actor,
        CancellationToken cancellationToken = default);

    Task<SmartAlertDto?> DismissAlertAsync(
        int tenantId,
        long id,
        string actor,
        CancellationToken cancellationToken = default);

    /// <summary>Persist + SignalR broadcast when newly created.</summary>
    Task<(SmartAlertDto Alert, bool Created)> RaiseAsync(
        CreateSmartAlertRequest request,
        CancellationToken cancellationToken = default);
}
