using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.DriverAllowance.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for driver allowance rules. SQL lives in Infrastructure.
/// </summary>
public interface IDriverAllowanceRepository
{
    Task<PagedResult<DriverAllowanceRuleDto>> GetPagedAsync(
        int page,
        int pageSize,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<DriverAllowanceRuleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(CreateDriverAllowanceRuleDto dto, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, UpdateDriverAllowanceRuleDto dto, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<DriverAllowanceRouteContext?> GetRouteContextAsync(int routeId, CancellationToken cancellationToken = default);

    Task<int?> GetVehicleFuelTypeAsync(int vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverAllowanceRuleDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default);
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
