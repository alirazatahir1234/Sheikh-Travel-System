using SheikhTravelSystem.Application.Features.TravelNexus.Configuration.DTOs;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// TravelNexus configuration. Every method except <see cref="GetCountriesAsync"/> is tenant-scoped
/// by the explicit <c>tenantId</c> argument (from <c>ITenantContext</c>, never from the client).
/// Save methods return null when the row does not exist for the tenant.
/// </summary>
public interface ITravelConfigRepository
{
    Task<IReadOnlyList<TravelCountryDto>> GetCountriesAsync(CancellationToken cancellationToken = default);
    Task<bool> CountryExistsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Active branches and active users of the tenant (id + name only).</summary>
    Task<TravelLookupsDto> GetLookupsAsync(int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelVisaTypeDto>> GetVisaTypesAsync(int tenantId, string? countryCode, bool? isActive, CancellationToken cancellationToken = default);
    Task<TravelVisaTypeDto?> GetVisaTypeByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> VisaTypeNameExistsAsync(int tenantId, string countryCode, string name, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> CreateVisaTypeAsync(int tenantId, SaveTravelVisaTypeDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateVisaTypeAsync(int id, int tenantId, SaveTravelVisaTypeDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteVisaTypeAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelDocumentTypeDto>> GetDocumentTypesAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default);
    Task<TravelDocumentTypeDto?> GetDocumentTypeByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> DocumentTypeCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> CreateDocumentTypeAsync(int tenantId, SaveTravelDocumentTypeDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateDocumentTypeAsync(int id, int tenantId, SaveTravelDocumentTypeDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentTypeAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelLeadSourceDto>> GetLeadSourcesAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default);
    Task<TravelLeadSourceDto?> GetLeadSourceByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> LeadSourceNameExistsAsync(int tenantId, string name, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> CreateLeadSourceAsync(int tenantId, SaveTravelLeadSourceDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateLeadSourceAsync(int id, int tenantId, SaveTravelLeadSourceDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteLeadSourceAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TravelLostReasonDto>> GetLostReasonsAsync(int tenantId, bool? isActive, CancellationToken cancellationToken = default);
    Task<TravelLostReasonDto?> GetLostReasonByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default);
    Task<bool> LostReasonNameExistsAsync(int tenantId, string name, int? excludeId, CancellationToken cancellationToken = default);
    Task<int> CreateLostReasonAsync(int tenantId, SaveTravelLostReasonDto dto, int? userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateLostReasonAsync(int id, int tenantId, SaveTravelLostReasonDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteLostReasonAsync(int id, int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns defaults when the tenant has never saved settings.</summary>
    Task<TravelSettingsDto> GetSettingsAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>Active airports (platform reference data): by search text, or exactly the given IATA codes.</summary>
    Task<IReadOnlyList<Features.TravelNexus.Suppliers.DTOs.TravelAirportDto>> GetAirportsAsync(string? search, IReadOnlyList<string>? codes,
        CancellationToken cancellationToken = default);
    Task UpsertSettingsAsync(int tenantId, UpdateTravelSettingsDto dto, int? userId, CancellationToken cancellationToken = default);
}
