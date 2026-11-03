namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for tenant queries and branding. SQL lives in Infrastructure.
/// </summary>
public interface ITenantRepository
{
    Task<TenantBrandingRow?> GetBrandingAsync(int tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts company contact / locale fields on TenantBranding.
    /// Throws <see cref="Exceptions.NotFoundException"/> when the tenant is missing.
    /// </summary>
    Task UpdateBrandingAsync(
        int tenantId,
        string? website,
        string? supportEmail,
        string? country,
        string? currencyCode,
        string? timeZone,
        CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);
}

public sealed record TenantBrandingRow(
    int Id,
    string Name,
    string Slug,
    string? TimeZone = null);
