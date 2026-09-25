using System.Security.Cryptography;
using System.Text;

namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

public static class TrackingToken
{
    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public static byte[] Hash(string token)
    {
        var raw = Encoding.UTF8.GetBytes(token.Trim());
        return SHA256.HashData(raw);
    }

    public static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        var s = Convert.ToBase64String(data);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
