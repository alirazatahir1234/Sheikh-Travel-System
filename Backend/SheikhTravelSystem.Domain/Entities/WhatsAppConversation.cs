using SheikhTravelSystem.Domain.Common;

namespace SheikhTravelSystem.Domain.Entities;

/// <summary>Thread between a business WhatsApp account and a customer phone.</summary>
public class WhatsAppConversation : BaseEntity
{
    public int WhatsAppAccountId { get; set; }
    public string CustomerPhoneNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public int? CustomerId { get; set; }
    public int? LeadId { get; set; }
    public int? AssignedUserId { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime? LastMessageAt { get; set; }
    public DateTime? LastIncomingMessageAt { get; set; }
    public DateTime? LastOutgoingMessageAt { get; set; }
    public int UnreadCount { get; set; }
    public bool IsBotEnabled { get; set; }
    public string? CurrentBotState { get; set; }
}
