namespace SheikhTravelSystem.Application.Features.Ocr.DTOs;

public enum OcrMode
{
    Hybrid = 0,
    PaddleOnly = 1,
    AzureOnly = 2
}

public record ExtractIdentityOcrRequest(
    OcrMode Mode = OcrMode.Hybrid,
    int ConfidenceThreshold = 70,
    bool EnableFallback = true,
    /// <summary>When false, raw OCR text is omitted from the API response (cleaner client UI).</summary>
    bool IncludeRawText = true);

public record IdentityOcrResult(
    string Provider,
    int Confidence,
    bool FallbackUsed,
    string? FullName,
    string? IdentityNumber,
    string? Address,
    string? DateOfBirth,
    string? Gender,
    string? RawText,
    string? FatherName = null,
    string? Nationality = null,
    bool AddressTranslated = false);

/// <summary>Sanitized OCR payload for clients (no internal provider chains).</summary>
public record IdentityOcrApiResponse(
    string? FullName,
    string? FatherName,
    string? IdentityNumber,
    string? Gender,
    string? DateOfBirth,
    string? Nationality,
    string? Address,
    string OcrEngine,
    int Confidence,
    bool FallbackUsed,
    string? RawText,
    bool LowConfidence,
    int ConfidenceThreshold,
    bool AddressTranslated,
    bool AzureQualityMergeUsed,
    string? PrimaryOcrEngine,
    string? SecondaryOcrEngine);

public static class IdentityOcrApiMapper
{
    public static IdentityOcrApiResponse ToApiResponse(
        IdentityOcrResult r,
        OcrMode mode,
        bool includeRawText,
        int confidenceThreshold)
    {
        var threshold = Math.Clamp(confidenceThreshold, 1, 99);
        var low = r.Confidence > 0 && r.Confidence < threshold;
        var azureMerge = r.Provider.Contains('→', StringComparison.Ordinal) && !r.FallbackUsed;
        var (primary, secondary) = MapPrimarySecondary(r, mode, azureMerge);
        return new IdentityOcrApiResponse(
            r.FullName,
            r.FatherName,
            r.IdentityNumber,
            r.Gender,
            r.DateOfBirth,
            r.Nationality,
            r.Address,
            MapDisplayOcrEngine(r, mode, azureMerge),
            r.Confidence,
            r.FallbackUsed,
            includeRawText ? r.RawText : null,
            low,
            threshold,
            r.AddressTranslated,
            azureMerge,
            primary,
            secondary);
    }

    private static (string? Primary, string? Secondary) MapPrimarySecondary(
        IdentityOcrResult r,
        OcrMode mode,
        bool azureMerge)
    {
        if (r.FallbackUsed)
            return ("PaddleOCR", "Azure AI (unavailable — Paddle only)");

        if (azureMerge && r.Provider.Contains('→', StringComparison.Ordinal))
        {
            var parts = r.Provider.Split('→', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (ShortProviderLabel(parts[0]), ShortProviderLabel(parts[^1]));
        }

        if (mode == OcrMode.PaddleOnly)
            return ("PaddleOCR", null);
        if (mode == OcrMode.AzureOnly)
            return ("Azure AI", null);

        return (ShortProviderLabel(r.Provider), null);
    }

    private static string ShortProviderLabel(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return "OCR";
        if (provider.Contains("Azure", StringComparison.OrdinalIgnoreCase))
            return "Azure AI";
        if (provider.Contains("Paddle", StringComparison.OrdinalIgnoreCase))
            return "PaddleOCR";
        return provider.Trim();
    }

    private static string MapDisplayOcrEngine(IdentityOcrResult r, OcrMode mode, bool azureMerge)
    {
        if (mode == OcrMode.Hybrid && (azureMerge || r.FallbackUsed))
            return "Hybrid";
        if (mode == OcrMode.PaddleOnly)
            return "PaddleOCR";
        if (mode == OcrMode.AzureOnly)
            return "Azure AI";
        if (azureMerge)
            return "Hybrid";
        if (string.Equals(r.Provider, "PaddleOCR", StringComparison.OrdinalIgnoreCase))
            return "PaddleOCR";
        if (r.Provider.Contains("Azure", StringComparison.OrdinalIgnoreCase))
            return "Azure AI";
        return "Hybrid";
    }
}
