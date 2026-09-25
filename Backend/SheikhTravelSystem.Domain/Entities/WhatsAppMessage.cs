using SheikhTravelSystem.Domain.Common;

namespace SheikhTravelSystem.Domain.Entities;

/// <summary>Inbound or outbound WhatsApp Cloud API message. MessageId is the Meta wamid for idempotency.</summary>
public class WhatsAppMessage : BaseEntity
{
    public int ConversationId { get; set; }
    public int WhatsAppAccountId { get; set; }

    /// <summary>Meta message id (wamid). Unique when present to prevent duplicate webhook inserts.</summary>
    public string? MessageId { get; set; }

    public string Direction { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text";
    public string? Text { get; set; }
    public string? MediaId { get; set; }
    public string? MediaUrl { get; set; }
    public string? TemplateName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; } = 1;
    public string? RawPayload { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
