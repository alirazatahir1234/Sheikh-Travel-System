using System.Text.RegularExpressions;
using SheikhTravelSystem.Application.Features.Ocr.DTOs;

namespace SheikhTravelSystem.Infrastructure.Services.Ocr;

internal static class OcrHeuristics
{
    private static readonly Regex CnicRegex = new(@"\b\d{5}-\d{7}-\d\b|\b\d{13}\b", RegexOptions.Compiled);

    public static IdentityOcrResult BuildFromFileName(string providerName, string fileName, int confidence)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
        var normalized = baseName.Replace('_', ' ').Replace('-', ' ').Trim();
        var cnic = ExtractAndNormalizeIdentityNumber(normalized);
        var fullName = GuessName(normalized);

        return new IdentityOcrResult(
            Provider: providerName,
            Confidence: confidence,
            FallbackUsed: false,
            FullName: fullName,
            IdentityNumber: cnic,
            Address: null,
            DateOfBirth: null,
            Gender: null,
            RawText: normalized);
    }

    public static string? ExtractAndNormalizeIdentityNumber(string text)
    {
        var match = CnicRegex.Match(text);
        if (!match.Success) return null;

        var value = match.Value;
        if (value.Contains('-')) return value;
        return $"{value[..5]}-{value.Substring(5, 7)}-{value.Substring(12, 1)}";
    }

    private static string? GuessName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 1 && p.All(ch => char.IsLetter(ch)))
            .Take(4)
            .ToArray();
        if (parts.Length < 2) return null;
        return string.Join(' ', parts);
    }
}
