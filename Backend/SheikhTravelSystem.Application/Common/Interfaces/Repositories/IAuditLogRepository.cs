using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.AuditLogs.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task<PagedResult<AuditLogDto>> GetPagedAsync(
        int page,
        int pageSize,
        int tenantId,
        string? action,
        string? entityName,
        int? userId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);
}
