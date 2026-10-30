using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

/// <summary>
/// Local-disk stand-in for playback coordinate storage (dev). Paths stay under a private playback root;
/// "signed" URLs are absolute API-relative paths with an expiry token query for parity with Azure SAS.
/// </summary>
public sealed class LocalPlaybackCoordinateStorage : IPlaybackCoordinateStorage
{
    private readonly string _rootPath;
    private readonly string? _publicOrigin;
    private readonly FileStorageOptions _options;

    public LocalPlaybackCoordinateStorage(IHostEnvironment env, IOptions<FileStorageOptions> options)
    {
        _options = options.Value;
        var root = Path.Combine(
            Path.IsPathRooted(_options.RootPath) ? _options.RootPath : Path.Combine(env.ContentRootPath, _options.RootPath),
            ContainerName);
        _rootPath = root;
        _publicOrigin = string.IsNullOrWhiteSpace(_options.PublicOrigin) ? null : _options.PublicOrigin.TrimEnd('/');
        Directory.CreateDirectory(_rootPath);
    }

    public string ContainerName =>
        string.IsNullOrWhiteSpace(_options.PlaybackContainerName)
            ? "gps-playbacks"
            : _options.PlaybackContainerName.Trim();

    public async Task<PlaybackStoredFileResult> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        var storageKey = BuildStorageKey(folder, fileName);
        var relativePath = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            if (content.CanSeek)
                content.Position = 0;
            await content.CopyToAsync(fs, cancellationToken);
        }

        var size = new FileInfo(fullPath).Length;
        var expiresOn = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.PlaybackSasExpiryMinutes, 5, 24 * 60));
        var signedUrl = BuildSignedReadUrl(storageKey, expiresOn);

        return new PlaybackStoredFileResult(
            storageKey,
            signedUrl,
            expiresOn,
            Path.GetFileName(storageKey),
            size,
            contentType);
    }

    public string ResolveSignedReadUrl(string storageKey)
    {
        var key = storageKey.Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var expiresOn = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.PlaybackSasExpiryMinutes, 5, 24 * 60));
        return BuildSignedReadUrl(key, expiresOn);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var key = storageKey.Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(key))
            return Task.CompletedTask;

        var fullPath = Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    private string BuildSignedReadUrl(string storageKey, DateTimeOffset expiresOn)
    {
        // Local parity: path under uploads/gps-playbacks with expiry query (not a public CDN URL).
        var path = $"{_options.PublicBasePath.TrimEnd('/')}/{ContainerName}/{storageKey}";
        var url = _publicOrigin is not null ? $"{_publicOrigin}{path}" : path;
        return $"{url}?expires={Uri.EscapeDataString(expiresOn.ToUnixTimeSeconds().ToString())}";
    }

    private static string BuildStorageKey(string folder, string fileName)
    {
        var safeFolder = string.Join('/', folder
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => string.Join('_', part.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))));

        var ext = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var safeBase = string.Join('_', (baseName ?? "playback")
            .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeBase))
            safeBase = "playback";

        var storedName = $"{safeBase}-{Guid.NewGuid():N}{ext}".ToLowerInvariant();
        return string.IsNullOrWhiteSpace(safeFolder) ? storedName : $"{safeFolder}/{storedName}";
    }
}
