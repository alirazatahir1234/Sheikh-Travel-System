using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Settlements.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public interface IDriverSettlementRepository
{
    Task<PagedResult<SettlementDto>> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        int? driverId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<SettlementDetailDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<bool> DriverExistsAsync(int driverId, int tenantId, CancellationToken cancellationToken = default);

    Task<SettlementTotals> CalculateTotalsAsync(
        int tenantId,
        int driverId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettlementLineInput>> BuildLinesAsync(
        int tenantId,
        int driverId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default);

    Task<int> CreateDraftAsync(
        int tenantId,
        int? createdBy,
        int driverId,
        DateTime periodStart,
        DateTime periodEnd,
        SettlementTotals totals,
        decimal adjustments,
        decimal payable,
        IReadOnlyList<SettlementLineInput> lines,
        CancellationToken cancellationToken = default);

    Task<SettlementDetailDto?> GetHeaderAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task ApproveAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task MarkPaidAsync(int id, int tenantId, string? reference, CancellationToken cancellationToken = default);

    Task AdjustAsync(
        int id,
        int tenantId,
        decimal adjustments,
        decimal payable,
        string? notes,
        CancellationToken cancellationToken = default);
}
