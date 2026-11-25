using SheikhTravelSystem.Application.Features.TravelNexus.Hotels.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Hotel catalog. Every statement is scoped by <c>tenantId</c>.</summary>
public interface ITravelHotelRepository
{
    Task<IReadOnlyList<TravelHotelDto>> GetListAsync(int tenantId, string? city, bool? isActive, CancellationToken cancellationToken = default);
    Task<TravelHotelDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(int tenantId, string name, string city, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(int tenantId, SaveTravelHotelDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, int tenantId, SaveTravelHotelDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, int tenantId, CancellationToken cancellationToken = default);
}
