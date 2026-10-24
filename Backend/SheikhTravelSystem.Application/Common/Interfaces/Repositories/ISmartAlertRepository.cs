using SheikhTravelSystem.Application.Features.SmartOps;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface ISmartAlertRepository
{
    /// <summary>
    /// Inserts when DeduplicationKey is new for the tenant. Returns the row (new or existing active match).
    /// </summary>
    Task<(SmartAlertDto Alert, bool Created)> TryInsertAsync(
        CreateSmartAlertRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SmartAlertDto>> GetActiveAsync(
        int tenantId,
        SmartAlertRuleType? ruleType = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<int> CountNewAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<SmartAlertDto?> GetByIdAsync(int tenantId, long id, CancellationToken cancellationToken = default);

    Task<bool> AcknowledgeAsync(int tenantId, long id, string acknowledgedBy, CancellationToken cancellationToken = default);

    Task<bool> ResolveAsync(int tenantId, long id, string resolvedBy, CancellationToken cancellationToken = default);

    Task<bool> DismissAsync(int tenantId, long id, string dismissedBy, CancellationToken cancellationToken = default);
}
