using SheikhTravelSystem.Domain.Common;

namespace SheikhTravelSystem.Domain.Entities;

/// <summary>Local catalog of Meta WhatsApp message templates (approval tracked locally).</summary>
public class WhatsAppTemplate : BaseEntity
{
    public int WhatsAppAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string Category { get; set; } = "UTILITY";
    public string Status { get; set; } = "Draft";
    public string? MetaTemplateId { get; set; }
    public string? BodyPreview { get; set; }
}
