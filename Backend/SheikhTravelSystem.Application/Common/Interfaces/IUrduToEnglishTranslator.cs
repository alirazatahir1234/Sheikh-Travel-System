namespace SheikhTravelSystem.Application.Common.Interfaces;

/// <summary>Translates Urdu (or mixed) address text to English (e.g. Azure Translator).</summary>
public interface IUrduToEnglishTranslator
{
    Task<string?> TranslateUrduToEnglishAsync(string text, CancellationToken cancellationToken = default);
}
