using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

/// <summary>
/// Local travel document storage for development. Root is outside the publicly served <c>uploads</c> folder;
/// keys are validated against a strict pattern so they can never escape the root.
/// </summary>
public sealed class LocalTravelDocumentStorage : ITravelDocumentStorage
{
    private readonly string _root;

    public LocalTravelDocumentStorage(IHostEnvironment environment, IOptions<TravelDocumentStorageOptions> options)
    {
        var configured = string.IsNullOrWhiteSpace(options.Value.LocalRoot) ? "private-storage/travel-documents" : options.Value.LocalRoot;
        _root = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured));
    }

    /// <summary>Test hook: explicit root.</summary>
    internal LocalTravelDocumentStorage(string root) => _root = Path.GetFullPath(root);

    public async Task<string> SaveAsync(byte[] content, string contentType, string extension, int tenantId, int visaCaseId,
        CancellationToken cancellationToken = default)
    {
        var key = TravelDocumentStorageKeys.Build(tenantId, visaCaseId, extension);
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return key;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream>(new FileStream(Resolve(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (TravelDocumentStorageKeys.IsValid(storageKey))
        {
            var path = Resolve(storageKey);
            if (File.Exists(path)) File.Delete(path);
        }
        return Task.CompletedTask;
    }

    internal string Resolve(string key)
    {
        if (!TravelDocumentStorageKeys.IsValid(key))
            throw new InvalidOperationException("Invalid travel document key.");
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid travel document key.");
        return full;
    }
}
