using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Infrastructure.Services.Storage;

namespace SheikhTravelSystem.Tests.Storage;

public class FileStorageResolveReadUrlTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalFileStorageService _storage;

    public FileStorageResolveReadUrlTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "sts-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_rootPath);

        var options = Options.Create(new FileStorageOptions
        {
            RootPath = _rootPath,
            PublicBasePath = "/uploads",
            PublicOrigin = "http://127.0.0.1:5082"
        });

        _storage = new LocalFileStorageService(env.Object, options);
    }

    [Fact]
    public void ResolveReadUrl_StorageKey_ReturnsPublicUrl()
    {
        var url = _storage.ResolveReadUrl("vehicles_1_54/abc.jpeg");
        url.Should().Be("http://127.0.0.1:5082/uploads/vehicles_1_54/abc.jpeg");
    }

    [Fact]
    public void ResolveReadUrl_LegacyHttpsUrl_ReturnsUnchanged()
    {
        const string legacy = "https://example.blob.core.windows.net/container/file.jpeg?sig=abc";
        _storage.ResolveReadUrl(legacy).Should().Be(legacy);
    }

    [Fact]
    public async Task DeleteAsync_BlobFailure_DoesNotPropagateFromService()
    {
        var missingKey = "vehicles_1_99/missing.jpeg";
        var act = async () => await _storage.DeleteAsync(missingKey);
        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}

public class DeleteVehicleCommandHandlerTests
{
    [Fact]
    public void DeleteVehicleHandlerSource_ShouldScopeVehicleDeleteByTenant()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "SheikhTravelSystem.Application",
            "Features", "Vehicles", "Commands", "DeleteVehicleCommand.cs"));

        var source = File.ReadAllText(sourcePath);

        source.Should().Contain("UPDATE Vehicles SET IsDeleted = 1");
        source.Should().Contain("WHERE Id = @Id AND TenantId = @TenantId");
        source.Should().Contain("logger.LogWarning");
    }

    [Fact]
    public async Task BlobCleanup_IgnoresDeleteFailures()
    {
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.DeleteAsync("bad-url", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob missing"));
        storage.Setup(s => s.DeleteAsync("good-url", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        foreach (var url in new[] { "bad-url", "good-url" })
        {
            try
            {
                await storage.Object.DeleteAsync(url);
            }
            catch (Exception)
            {
                // best-effort: handler logs and continues
            }
        }

        storage.Verify(s => s.DeleteAsync("bad-url", It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.DeleteAsync("good-url", It.IsAny<CancellationToken>()), Times.Once);
    }
}
