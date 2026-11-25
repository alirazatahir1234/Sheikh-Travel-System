using System.Security.Cryptography;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Documents;

public sealed record TravelDocumentFileCheck(bool IsValid, string? Error, string ContentType, string Extension, string Sha256)
{
    public static TravelDocumentFileCheck Fail(string error) => new(false, error, "", "", "");
}

/// <summary>
/// Upload validation for applicant documents: extension allowlist, magic-byte sniffing that must agree with the
/// extension, size limit and SHA-256. The stored content type always comes from the sniffed bytes, never the client.
/// </summary>
public static class TravelDocumentFileValidator
{
    public const int MaxBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> ExtensionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
    };

    public static IReadOnlyCollection<string> AllowedExtensions => ExtensionTypes.Keys;

    public static TravelDocumentFileCheck Validate(string? fileName, byte[] content)
    {
        if (content.Length == 0) return TravelDocumentFileCheck.Fail("The file is empty.");
        if (content.Length > MaxBytes) return TravelDocumentFileCheck.Fail("The file is larger than 10 MB.");

        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!ExtensionTypes.TryGetValue(extension, out var expectedType))
            return TravelDocumentFileCheck.Fail("Only PDF, JPG and PNG files are accepted.");

        var sniffed = Sniff(content);
        if (sniffed is null)
            return TravelDocumentFileCheck.Fail("The file content is not a valid PDF, JPG or PNG.");
        if (sniffed != expectedType)
            return TravelDocumentFileCheck.Fail("The file content does not match its extension.");

        return new TravelDocumentFileCheck(true, null, sniffed, extension == ".jpeg" ? ".jpg" : extension,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
    }

    /// <summary>Content type from the file signature, or null when unrecognised.</summary>
    public static string? Sniff(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 5 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46 && b[4] == 0x2D) return "application/pdf"; // %PDF-
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        return null;
    }
}
