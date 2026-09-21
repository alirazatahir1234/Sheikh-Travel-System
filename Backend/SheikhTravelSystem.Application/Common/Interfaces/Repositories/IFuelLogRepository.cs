using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Features.FuelLogs.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for fuel logs. SQL lives in Infrastructure.
/// </summary>
public interface IFuelLogRepository
{
    Task<PagedResult<FuelLogDto>> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<FuelLogDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(CreateFuelLogDto dto, CancellationToken cancellationToken = default);

    Task<int> UpdateAsync(int id, CreateFuelLogDto dto, CancellationToken cancellationToken = default);

    Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
