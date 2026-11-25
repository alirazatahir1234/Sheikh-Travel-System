using System.Text.RegularExpressions;

namespace SheikhTravelSystem.Infrastructure.Services.Storage;

/// <summary>Key format shared by Azure and local travel document storage: <c>tenant-{id}/case-{id}/{guid}{ext}</c>.</summary>
internal static partial class TravelDocumentStorageKeys
{
    public static string Build(int tenantId, int visaCaseId, string extension)
        => $"tenant-{tenantId}/case-{visaCaseId}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

    /// <summary>Only keys produced by <see cref="Build"/> are accepted (no traversal, no absolute paths).</summary>
    public static bool IsValid(string? key) => key is not null && KeyRegex().IsMatch(key);

    [GeneratedRegex(@"^tenant-\d+/case-\d+/[0-9a-f]{32}\.(pdf|jpg|jpeg|png)$")]
    private static partial Regex KeyRegex();
}
