using System.Text.Json.Serialization;

namespace SheikhTravelSystem.Application.Features.WhatsApp;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public bool Enabled { get; set; }

    /// <summary>Meta webhook verify token. Prefer WebhookVerifyToken env alias.</summary>
    public string VerifyToken { get; set; } = "";

    /// <summary>Env alias: WhatsApp__WebhookVerifyToken</summary>
    public string WebhookVerifyToken { get; set; } = "";

    public string AppSecret { get; set; } = "";
    public string GraphApiVersion { get; set; } = "v21.0";
    public int MessageRetentionDays { get; set; } = 180;

    /// <summary>
    /// Utility template used by notification channel when the 24h messaging window is closed.
    /// Must be Approved in WhatsAppTemplates; otherwise the channel fails (no fake success).
    /// </summary>
    public string NotificationTemplateName { get; set; } = "support_update";

    public string NotificationTemplateLanguage { get; set; } = "en";

    /// <summary>Optional route used when WhatsApp self-service creates a booking.</summary>
    public int SelfServiceDefaultRouteId { get; set; }

    /// <summary>Placeholder fare until ops confirms (PKR).</summary>
    public decimal SelfServiceDefaultAmount { get; set; } = 1000m;

    /// <summary>Env: WhatsApp__Uae__*</summary>
    public WhatsAppAccountOptions Uae { get; set; } = new();

    /// <summary>Env: WhatsApp__Pakistan__*</summary>
    public WhatsAppAccountOptions Pakistan { get; set; } = new();

    /// <summary>Env: WhatsApp__Accounts__UAE|PK__* (legacy / explicit codes)</summary>
    public Dictionary<string, WhatsAppAccountOptions> Accounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string EffectiveVerifyToken
        => FirstNonEmpty(WebhookVerifyToken, VerifyToken) ?? "";

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}

public sealed class WhatsAppAccountOptions
{
    public string PhoneNumberId { get; set; } = "";
    public string AccessToken { get; set; } = "";

    public string BusinessAccountId { get; set; } = "";

    /// <summary>Legacy config key; prefer BusinessAccountId.</summary>
    [JsonPropertyName("WabaId")]
    public string WabaId { get; set; } = "";

    public string EffectiveBusinessAccountId
        => FirstNonEmpty(BusinessAccountId, WabaId) ?? "";

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
