namespace SheikhTravelSystem.Infrastructure.Services.Ocr;

public class OcrOptions
{
    public const string SectionName = "Ocr";

    public int ConfidenceThreshold { get; set; } = 70;
    public bool EnableFallback { get; set; } = true;
    public string? AzureEndpoint { get; set; }
    public string? AzureApiKey { get; set; }
    /// <summary>Document Intelligence polling can exceed a few seconds; keep this generous for dev.</summary>
    public int AzureTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Base URL of the optional PaddleOCR HTTP sidecar (e.g. http://127.0.0.1:8088).
    /// POST multipart file field "file" to {BaseUrl}/ocr. When empty, Paddle falls back to filename heuristics only.
    /// </summary>
    public string? PaddleOcrServiceUrl { get; set; }

    public int PaddleOcrTimeoutSeconds { get; set; } = 120;
}
