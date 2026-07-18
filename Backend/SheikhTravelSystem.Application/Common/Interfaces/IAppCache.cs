namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>
/// Application-level cache abstraction. Uses in-memory on a single VM and
/// distributed Redis when configured.
/// </summary>
public interface IAppCache
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default);

    void Remove(string key);

    void RemoveByPrefix(string prefix);
}
