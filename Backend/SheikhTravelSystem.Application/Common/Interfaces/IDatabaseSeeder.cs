namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Seeds baseline demo/reference data into the database on application startup.
/// Implementations must be idempotent — running the seeder multiple times
/// should not create duplicate rows.
/// </summary>
public interface IDatabaseSeeder
{
    /// <summary>
    /// Seeds empty tables. Safe to run repeatedly — does nothing if data exists.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Wipes every seedable table (respecting FK order) and re-seeds from scratch.
    /// Intended for development only.
    /// </summary>
    Task ResetAndSeedAsync(CancellationToken cancellationToken = default);
}
