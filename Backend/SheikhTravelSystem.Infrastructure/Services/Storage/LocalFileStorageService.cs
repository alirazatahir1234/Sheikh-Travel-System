using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly string _publicBasePath;
    private readonly string? _publicOrigin;

    public LocalFileStorageService(IHostEnvironment env, IOptions<FileStorageOptions> options)
    {
        var opts = options.Value;
        _rootPath = Path.IsPathRooted(opts.RootPath)
            ? opts.RootPath
            : Path.Combine(env.ContentRootPath, opts.RootPath);
        _publicBasePath = opts.PublicBasePath.TrimEnd('/');
        _publicOrigin = string.IsNullOrWhiteSpace(opts.PublicOrigin) ? null : opts.PublicOrigin.TrimEnd('/');
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFileResult> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var safeFolder = string.Join('_', folder.Split(Path.GetInvalidPathChars(), StringSplitOptions.RemoveEmptyEntries));
        var ext = Path.GetExtension(fileName);
        var storedName = $"{Guid.NewGuid():N}{ext}".ToLowerInvariant();
        var storageKey = $"{safeFolder}/{storedName}".Replace('\\', '/');
        var absolutePath = Path.Combine(_rootPath, safeFolder);
        Directory.CreateDirectory(absolutePath);
        var fullPath = Path.Combine(absolutePath, storedName);

        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, cancellationToken);

        var readUrl = BuildReadUrl(storageKey);
        return new StoredFileResult(storageKey, readUrl, storedName, fs.Length, contentType);
    }

    public Task DeleteAsync(string storageKeyOrLegacyUrl, CancellationToken cancellationToken = default)
    {
        var storageKey = ResolveStorageKey(storageKeyOrLegacyUrl);
        if (string.IsNullOrWhiteSpace(storageKey))
            return Task.CompletedTask;

        var relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(_rootPath, relativePath);
        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        return Task.CompletedTask;
    }

    public string ResolveReadUrl(string? storageKeyOrLegacyUrl)
    {
        if (string.IsNullOrWhiteSpace(storageKeyOrLegacyUrl))
            return string.Empty;

        if (IsLegacyUrl(storageKeyOrLegacyUrl))
            return storageKeyOrLegacyUrl;

        return BuildReadUrl(storageKeyOrLegacyUrl.TrimStart('/'));
    }

    private string BuildReadUrl(string storageKey)
    {
        var publicPath = $"{_publicBasePath}/{storageKey}";
        return _publicOrigin is not null ? $"{_publicOrigin}{publicPath}" : publicPath;
    }

    private string? ResolveStorageKey(string storageKeyOrLegacyUrl)
    {
        if (string.IsNullOrWhiteSpace(storageKeyOrLegacyUrl))
            return null;

        if (IsLegacyUrl(storageKeyOrLegacyUrl))
        {
            var prefix = _publicBasePath + '/';
            if (storageKeyOrLegacyUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return storageKeyOrLegacyUrl[prefix.Length..];

            if (_publicOrigin is not null)
            {
                var fullPrefix = _publicOrigin + prefix;
                if (storageKeyOrLegacyUrl.StartsWith(fullPrefix, StringComparison.OrdinalIgnoreCase))
                    return storageKeyOrLegacyUrl[fullPrefix.Length..];
            }

            return null;
        }

        return storageKeyOrLegacyUrl.TrimStart('/');
    }

    private static bool IsLegacyUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase);
}
