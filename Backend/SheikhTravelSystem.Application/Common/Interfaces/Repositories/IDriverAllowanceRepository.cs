using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for driver allowance rules. SQL lives in Infrastructure.
/// </summary>
public interface IDriverAllowanceRepository
{
    Task<PagedResult<DriverAllowanceRuleDto>> GetPagedAsync(
        int tenantId,
        int page,
        int pageSize,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<DriverAllowanceRuleDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(int tenantId, CreateDriverAllowanceRuleDto dto, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, int tenantId, UpdateDriverAllowanceRuleDto dto, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<DriverAllowanceRouteContext?> GetRouteContextAsync(int routeId, int tenantId, CancellationToken cancellationToken = default);

    Task<int?> GetVehicleFuelTypeAsync(int vehicleId, int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverAllowanceRuleDto>> GetActiveRulesAsync(int tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Route fields needed by the allowance evaluator.
/// </summary>
public sealed class DriverAllowanceRouteContext
{
    public decimal Distance { get; set; }
    public string? Name { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
}
