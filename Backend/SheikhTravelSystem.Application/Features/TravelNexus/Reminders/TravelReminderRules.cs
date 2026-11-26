using System.Globalization;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reminders;

/// <summary>Pure reminder rules: effective configuration, firing windows, dedupe keys, quiet hours.</summary>
public static class TravelReminderRules
{
    public sealed record EffectiveLead(TravelReminderDefinition Definition, TravelReminderLeadDto Lead, string? CustomerTemplate);

    /// <summary>Catalogue defaults, replaced per rule type by the tenant's rows. Disabled rows are dropped.</summary>
    public static IReadOnlyList<TravelReminderRuleDto> Merge(IReadOnlyList<TravelReminderRuleRow> rows)
        => TravelReminderCatalog.All.Select(d =>
        {
            var own = rows.Where(r => r.RuleType == d.RuleType).OrderBy(r => r.LeadDays).ToList();
            return new TravelReminderRuleDto
            {
                RuleType = d.RuleType, Name = d.Name, Description = d.Description, EntityType = d.EntityType, Kind = d.Kind.ToString(),
                SupportsStaff = d.SupportsStaff, SupportsCustomer = d.SupportsCustomer, IsCustomised = own.Count > 0,
                CustomerTemplate = own.Select(r => r.CustomerTemplate).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)), Placeholders = TravelReminderCatalog.Placeholders,
                Leads = own.Count > 0
                    ? own.Select(r => new TravelReminderLeadDto
                    {
                        LeadDays = r.LeadDays, IsEnabled = r.IsEnabled, StaffInApp = r.StaffInApp && d.SupportsStaff, StaffEmail = r.StaffEmail && d.SupportsStaff,
                        CustomerEmail = r.CustomerEmail && d.SupportsCustomer, CustomerSms = r.CustomerSms && d.SupportsCustomer,
                        CustomerWhatsApp = r.CustomerWhatsApp && d.SupportsCustomer
                    }).ToList()
                    : d.DefaultLeads.Select(d.DefaultLead).ToList()
            };
        }).ToList();

    /// <summary>The enabled lead times the scanner evaluates (a rule type with every row disabled is off).</summary>
    public static IReadOnlyList<EffectiveLead> Effective(IReadOnlyList<TravelReminderRuleRow> rows)
        => Merge(rows).SelectMany(rule => rule.Leads.Where(l => l.IsEnabled && HasAnyChannel(l))
            .Select(l => new EffectiveLead(TravelReminderCatalog.Find(rule.RuleType)!, l, rule.CustomerTemplate))).ToList();

    public static bool HasAnyChannel(TravelReminderLeadDto l) => l.StaffInApp || l.StaffEmail || l.CustomerEmail || l.CustomerSms || l.CustomerWhatsApp;

    public static IReadOnlyList<string> StaffChannels(TravelReminderLeadDto l)
        => new[] { l.StaffInApp ? "InApp" : null, l.StaffEmail ? "Email" : null }.Where(c => c is not null).Select(c => c!).ToList();

    public static IReadOnlyList<string> CustomerChannels(TravelReminderLeadDto l)
        => new[] { l.CustomerEmail ? "Email" : null, l.CustomerSms ? "Sms" : null, l.CustomerWhatsApp ? "WhatsApp" : null }
            .Where(c => c is not null).Select(c => c!).ToList();

    /// <summary>The trigger-date window the repository should return candidates for.</summary>
    public static (DateTime From, DateTime To) Window(TravelReminderDefinition d, int leadDays, DateTime nowUtc) => d.Kind switch
    {
        TravelReminderKind.Upcoming => (nowUtc, nowUtc.AddDays(leadDays).Date.AddDays(1).AddTicks(-1)),
        TravelReminderKind.Overdue => (DateTime.MinValue, nowUtc),
        _ => (nowUtc.AddHours(-48), nowUtc)
    };

    /// <summary>Is the candidate's trigger inside the firing window for this lead time?</summary>
    public static bool IsDue(TravelReminderDefinition d, int leadDays, DateTime dueAt, DateTime nowUtc)
    {
        var (from, to) = Window(d, leadDays, nowUtc);
        return dueAt >= from && dueAt <= to;
    }

    /// <summary>
    /// One reminder per rule, entity, trigger, lead time and audience. Upcoming rules key on the trigger date (a moved date fires
    /// again); weekly overdue rules on the ISO week, daily ones on the day; event rules on the candidate's stamp.
    /// </summary>
    public static string DedupeKey(TravelReminderDefinition d, int leadDays, TravelReminderCandidate c, string audience, DateTime nowUtc)
    {
        var stamp = d.Kind switch
        {
            TravelReminderKind.Upcoming => c.DueAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TravelReminderKind.Overdue => d.WeeklyNudge ? WeekStamp(nowUtc) : "D" + nowUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => c.Stamp ?? c.DueAt.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
        };
        return $"{d.RuleType}:{c.EntityType}:{c.EntityId}:{stamp}:{leadDays}:{audience}";
    }

    public static string WeekStamp(DateTime utc)
        => $"W{ISOWeek.GetYear(utc)}-{ISOWeek.GetWeekOfYear(utc):00}";

    /// <summary>Quiet hours wrap midnight: start 21, end 8 means 21:00–07:59 is quiet. start == end means no quiet hours.</summary>
    public static bool InQuietHours(DateTime localNow, int startHour, int endHour)
    {
        if (startHour == endHour) return false;
        var h = localNow.Hour;
        return startHour < endHour ? h >= startHour && h < endHour : h >= startHour || h < endHour;
    }

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
}
