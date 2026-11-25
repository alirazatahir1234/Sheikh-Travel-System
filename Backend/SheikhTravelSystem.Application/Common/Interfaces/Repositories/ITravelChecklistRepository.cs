using SheikhTravelSystem.Application.Features.TravelNexus.Checklists.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>Versioned document checklists. Every method is scoped by <c>tenantId</c>. Only Draft templates are mutable.</summary>
public interface ITravelChecklistRepository
{
    Task<IReadOnlyList<TravelChecklistTemplateListItemDto>> GetListAsync(int tenantId, string? countryCode, string? status, CancellationToken cancellationToken = default);
    Task<TravelChecklistTemplateDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TravelChecklistItemDto>> GetItemsAsync(int templateId, int tenantId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(int tenantId, SaveTravelChecklistTemplateDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateDraftAsync(int id, int tenantId, SaveTravelChecklistTemplateDto dto, CancellationToken cancellationToken = default);
    /// <summary>Replaces all items of a Draft template (order = list order) in one transaction.</summary>
    Task<bool> ReplaceItemsAsync(int id, int tenantId, IReadOnlyList<SaveTravelChecklistItemDto> items, CancellationToken cancellationToken = default);
    /// <summary>Draft → Active, retiring the currently Active template of the same country/visa type, in one transaction.</summary>
    Task<bool> ActivateAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    /// <summary>Copies a non-draft template and its items into a new Draft with the next version number.</summary>
    Task<int?> NewVersionAsync(int id, int tenantId, int? userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDraftAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Active checklist for the visa type, else the country-wide one; null when none.</summary>
    Task<TravelChecklistSnapshotSource?> GetActiveForAsync(int tenantId, string countryCode, int visaTypeId, CancellationToken cancellationToken = default);
}
