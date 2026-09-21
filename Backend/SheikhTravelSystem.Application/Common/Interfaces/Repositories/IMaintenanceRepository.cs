using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.Maintenance.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for legacy Maintenance records. SQL lives in Infrastructure.
/// </summary>
public interface IMaintenanceRepository
{
    Task<int> CreateAsync(CreateMaintenanceDto dto, CancellationToken cancellationToken = default);

    Task UpdateAsync(int id, CreateMaintenanceDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(int id, MaintenanceStatus status, CancellationToken cancellationToken = default);

    Task<PagedResult<MaintenanceDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<MaintenanceDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
