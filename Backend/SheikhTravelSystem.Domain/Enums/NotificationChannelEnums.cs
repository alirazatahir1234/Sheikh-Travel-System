namespace SheikhTravelSystem.Domain.Enums;

public enum NotificationPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
}

/// <summary>Delivery channel for a notification (single primary channel per row).</summary>
public enum NotificationChannel
{
    InApp = 1,
    Email = 2,
    Sms = 3,
    Push = 4,
    Browser = 5,
    WhatsApp = 6
}

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3
}
