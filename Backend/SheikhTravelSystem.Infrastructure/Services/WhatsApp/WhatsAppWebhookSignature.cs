using System.Security.Cryptography;
using System.Text;

namespace SheikhTravelSystem.Infrastructure.Services.WhatsApp;

public static class WhatsAppWebhookSignature
{
    /// <summary>Validates Meta <c>X-Hub-Signature-256</c> = <c>sha256=hex</c>.</summary>
    public static bool IsValid(string? appSecret, string payload, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(appSecret)) return false;
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;

        var header = signatureHeader.Trim();
        if (!header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var expectedHex = header["sha256=".Length..].Trim();
        if (expectedHex.Length == 0) return false;

        var key = Encoding.UTF8.GetBytes(appSecret);
        var body = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, body);
        var actualHex = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actualHex),
            Encoding.ASCII.GetBytes(expectedHex.ToLowerInvariant()));
    }
}
