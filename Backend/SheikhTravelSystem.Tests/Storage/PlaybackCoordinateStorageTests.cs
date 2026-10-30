using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Infrastructure.Services.Storage;

namespace SheikhTravelSystem.Tests.Storage;

public class PlaybackCoordinateStorageTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalPlaybackCoordinateStorage _storage;

    public PlaybackCoordinateStorageTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "sts-playback-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_rootPath);

        var options = Options.Create(new FileStorageOptions
        {
            RootPath = _rootPath,
            PublicBasePath = "/uploads",
            PublicOrigin = "http://127.0.0.1:5082",
            PlaybackContainerName = "gps-playbacks",
            PlaybackSasExpiryMinutes = 45
        });

        _storage = new LocalPlaybackCoordinateStorage(env.Object, options);
    }

    [Fact]
    public void ContainerName_IsDedicatedPlaybackContainer()
    {
        _storage.ContainerName.Should().Be("gps-playbacks");
    }

    [Fact]
    public async Task SaveAsync_ReturnsTimeLimitedSignedUrl_UnderPlaybackContainer()
    {
        await using var content = new MemoryStream("{}"u8.ToArray());
        var before = DateTimeOffset.UtcNow;

        var stored = await _storage.SaveAsync(
            content,
            "gps-history-1-20260101-20260102.geojson",
            "application/geo+json",
            "tenant-1/vehicle-9/2026/01");

        stored.StorageKey.Should().Contain("tenant-1");
        stored.StorageKey.Should().EndWith(".geojson");
        stored.SignedUrl.Should().Contain("/uploads/gps-playbacks/");
        stored.SignedUrl.Should().Contain("expires=");
        stored.ExpiresOn.Should().BeAfter(before.AddMinutes(40));
        stored.ExpiresOn.Should().BeBefore(before.AddMinutes(50));

        // Must not look like a bare public CDN URL without expiry.
        stored.SignedUrl.Should().NotMatchRegex(@"^https://[^?]+\.(png|jpg|mp4)$");
    }

    [Fact]
    public void ResolveSignedReadUrl_IssuesFreshExpiry()
    {
        var url = _storage.ResolveSignedReadUrl("tenant-1/vehicle-9/file.geojson");
        url.Should().Contain("/uploads/gps-playbacks/tenant-1/vehicle-9/file.geojson");
        url.Should().Contain("expires=");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
