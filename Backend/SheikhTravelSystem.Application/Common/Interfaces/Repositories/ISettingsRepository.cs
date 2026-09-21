namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

/// <summary>
/// Persistence for platform/tenant settings. SQL lives in Infrastructure.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Loads key/value settings for a category, overlaying Security/Branding table data when applicable.
    /// </summary>
    Task<Dictionary<string, string?>> GetByCategoryAsync(
        int tenantId,
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts key/value settings for a category, routing Security/Branding keys to normalized tables.
    /// </summary>
    Task SaveByCategoryAsync(
        int tenantId,
        string category,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken cancellationToken = default);
}
