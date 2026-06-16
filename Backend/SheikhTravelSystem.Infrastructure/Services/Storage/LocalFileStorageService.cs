using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Common.Interfaces;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;
    private readonly string _publicBasePath;

    public LocalFileStorageService(IHostEnvironment env, IOptions<FileStorageOptions> options)
    {
        var opts = options.Value;
        _rootPath = Path.IsPathRooted(opts.RootPath)
            ? opts.RootPath
            : Path.Combine(env.ContentRootPath, opts.RootPath);
        _publicBasePath = opts.PublicBasePath.TrimEnd('/');
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
        var relativePath = Path.Combine(safeFolder, storedName).Replace('\\', '/');
        var absolutePath = Path.Combine(_rootPath, safeFolder);
        Directory.CreateDirectory(absolutePath);
        var fullPath = Path.Combine(absolutePath, storedName);

        await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, cancellationToken);

        return new StoredFileResult(
            $"{_publicBasePath}/{relativePath}",
            storedName,
            fs.Length,
            contentType);
    }
}
