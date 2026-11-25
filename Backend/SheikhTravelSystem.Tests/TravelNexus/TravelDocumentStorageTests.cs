using FluentAssertions;
using SheikhTravelSystem.Infrastructure.Services.Storage;

namespace SheikhTravelSystem.Tests.TravelNexus;

public class TravelDocumentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tn-docs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Saves_under_tenant_and_case_and_reads_back()
    {
        var storage = new LocalTravelDocumentStorage(_root);
        var key = await storage.SaveAsync([1, 2, 3], "application/pdf", ".PDF", tenantId: 7, visaCaseId: 42);

        key.Should().MatchRegex(@"^tenant-7/case-42/[0-9a-f]{32}\.pdf$");
        await using var stream = await storage.OpenReadAsync(key);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(1, 2, 3);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("tenant-7/case-42/../../../secret.pdf")]
    [InlineData("/tenant-7/case-42/0123456789abcdef0123456789abcdef.pdf")]
    [InlineData("tenant-7/case-42/0123456789abcdef0123456789abcdef.exe")]
    [InlineData("uploads/whatever.pdf")]
    public void Rejects_keys_not_produced_by_the_store(string key)
    {
        var storage = new LocalTravelDocumentStorage(_root);
        var act = () => storage.Resolve(key);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Default_local_root_is_outside_the_public_uploads_folder()
        => new TravelDocumentStorageOptions().LocalRoot.Should().NotStartWith("uploads").And.Contain("private-storage");
}
