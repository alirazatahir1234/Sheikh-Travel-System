namespace SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

public static class WhatsAppAiAssistOperations
{
    public const string Suggest = "Suggest";
    public const string Regenerate = "Regenerate";
    public const string Shorter = "Shorter";
    public const string Professional = "Professional";
    public const string Translate = "Translate";
    public const string Summary = "Summary";
}

public static class WhatsAppAiAssistConfidence
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
}

public record WhatsAppAiAssistSuggestRequest(
    string? PriorSuggestion = null,
    string? Instruction = null);

public record WhatsAppAiAssistTransformRequest(
    string Action,
    string? Text = null,
    string? TargetLanguage = null,
    string? PriorSuggestion = null);

public record WhatsAppAiAssistResultDto(
    string Suggestion,
    string Confidence,
    bool ReviewRecommended,
    string Provider,
    string? Model,
    int DurationMs,
    IReadOnlyList<string> UnavailableFacts,
    string Operation,
    string? ConfidenceReason = null);

public record WhatsAppAiActiveTripFactsDto(
    int TripId,
    string? TripNumber,
    int? BookingId,
    string? BookingNumber,
    string StatusLabel,
    string? DriverFirstName,
    string? VehicleDescription,
    string? PlateNumber,
    string? TrackingUrl);
