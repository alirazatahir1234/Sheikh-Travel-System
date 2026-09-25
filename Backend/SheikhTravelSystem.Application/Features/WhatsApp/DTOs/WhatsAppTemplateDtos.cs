namespace SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

public record WhatsAppTemplateDto(
    int Id,
    int WhatsAppAccountId,
    string AccountCode,
    string Name,
    string Language,
    string Category,
    string Status,
    string? MetaTemplateId,
    string? BodyPreview);

public static class WhatsAppTemplateStatuses
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Paused = "Paused";
    public const string Disabled = "Disabled";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Pending, Approved, Rejected, Paused, Disabled
    };
}
