namespace SheikhTravelSystem.Infrastructure.Services.Ocr;

public class TranslatorOptions
{
    public const string SectionName = "Translator";

    public string? Key { get; set; }
    public string? Region { get; set; }

    /// <summary>Azure Translator global endpoint.</summary>
    public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";
}
