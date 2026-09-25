namespace SheikhTravelSystem.Application.Features.WhatsApp.Automation;

public static class WaAutomationEventType
{
    public const string BookingConfirmed = "BookingConfirmed";
    public const string BookingRescheduled = "BookingRescheduled";
    public const string BookingCancelled = "BookingCancelled";
    public const string PickupReminder = "PickupReminder";
    public const string DriverAssigned = "DriverAssigned";
    public const string DriverEnRoute = "DriverEnRoute";
    public const string DriverArriving = "DriverArriving";
    public const string DriverArrived = "DriverArrived";
    public const string TripCompleted = "TripCompleted";
    public const string RentalReturnReminder = "RentalReturnReminder";

    public static readonly string[] All =
    [
        BookingConfirmed, BookingRescheduled, BookingCancelled, PickupReminder,
        DriverAssigned, DriverEnRoute, DriverArriving, DriverArrived,
        TripCompleted, RentalReturnReminder
    ];
}

public static class WaAutomationEventStatus
{
    public const byte Pending = 0;
    public const byte Processing = 1;
    public const byte Sent = 2;
    public const byte Skipped = 3;
    public const byte Failed = 4;
    public const byte Cancelled = 5;
}

public static class WaAutomationSkipReason
{
    public const string RuleDisabled = "RuleDisabled";
    public const string NoConsent = "NoConsent";
    public const string OptedOut = "OptedOut";
    public const string NotRelevant = "NotRelevant";
    public const string Stale = "Stale";
    public const string NoRecipient = "NoRecipient";
    public const string TemplateMissing = "TemplateMissing";
    public const string BookingCancelled = "BookingCancelled";
}

public static class AutomationPolicy
{
    public static readonly TimeSpan ArrivalStaleAfter = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan QuietHoursStart = TimeSpan.FromHours(22);
    public static readonly TimeSpan QuietHoursEnd = TimeSpan.FromHours(8);
    public const int MaxResend = 3;
    public const int MaxProcessAttempts = 5;
    public static readonly TimeSpan TrackingHardCap = TimeSpan.FromHours(12);
    public static readonly TimeSpan TrackingAfterComplete = TimeSpan.FromMinutes(30);
    public const double ArrivingMeters = 800;
    public const double ArrivedMeters = 100;
    public const double ArrivedMaxSpeedKmh = 5;
    public const double DefaultCitySpeedKmh = 25;
    public const double RoadFactor = 1.4;

    public static bool IsUrgentDefault(string eventType)
        => eventType is not WaAutomationEventType.RentalReturnReminder;

    public static DateTime ApplyQuietHours(DateTime dueAtUtc, TimeZoneInfo tenantTz, bool isUrgent)
    {
        if (isUrgent)
            return dueAtUtc;

        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(dueAtUtc, DateTimeKind.Utc), tenantTz);
        var tod = local.TimeOfDay;
        if (tod >= QuietHoursStart || tod < QuietHoursEnd)
        {
            var nextMorning = local.Date;
            if (tod >= QuietHoursStart)
                nextMorning = nextMorning.AddDays(1);
            nextMorning = nextMorning.Add(QuietHoursEnd);
            return TimeZoneInfo.ConvertTimeToUtc(nextMorning, tenantTz);
        }

        return dueAtUtc;
    }

    public static string DedupeKey(string eventType, params object?[] parts)
        => string.Join(':', new object?[] { eventType }.Concat(parts).Select(p => p?.ToString() ?? ""));

    public static string ClientMessageId(string dedupeKey, string recipientE164)
        => $"waauto:{dedupeKey}:{recipientE164}".Length <= 128
            ? $"waauto:{dedupeKey}:{recipientE164}"
            : $"waauto:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{dedupeKey}:{recipientE164}"))[..16])}";
}
