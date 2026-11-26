using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.TravelNexus.Reminders.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.TravelNexus.Reminders;

/// <summary>
/// One tenant's reminder pass: effective rules → candidates per rule and lead → claim the dedupe key (the log row) → send through
/// the platform notification pipeline → record the outcome. A claim that cannot be sent is recorded as Skipped (no recipient) or
/// Failed (provider error) with the reason, never retried blindly: the next pass only sees keys that were never claimed.
/// </summary>
public sealed class TravelReminderDispatcher(
    ITravelReminderRepository repository,
    ITravelConfigRepository config,
    INotificationService notifications,
    ITenantTimeZoneResolver timeZones,
    IAuditEngine audit,
    ILogger<TravelReminderDispatcher> logger,
    TimeProvider? clock = null) : ITravelReminderRunner
{
    public const string Module = "travel_reminder";
    public const string TemplateKey = "travel_reminder";
    public const string AuditEventKey = "travel.reminder.dispatch";
    public const string NoAgentReason = "No assigned agent";

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<TravelReminderRunResultDto> RunTenantAsync(int tenantId, CancellationToken cancellationToken)
    {
        var result = new TravelReminderRunResultDto();
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var rows = await repository.GetRulesAsync(tenantId, cancellationToken);
        var leads = TravelReminderRules.Effective(rows);
        if (leads.Count == 0) return result;

        var settings = await config.GetSettingsAsync(tenantId, cancellationToken);
        var zone = ResolveZone(await timeZones.GetTenantTimeZoneIdAsync(tenantId, cancellationToken));
        var quiet = TravelReminderRules.InQuietHours(TravelReminderRules.ToLocal(nowUtc, zone), settings.ReminderQuietStartHour, settings.ReminderQuietEndHour);

        // Candidates are fetched once per rule type over the widest lead window, then matched per lead.
        var byRule = leads.GroupBy(l => l.Definition.RuleType);
        foreach (var group in byRule)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var definition = group.First().Definition;
            var maxLead = group.Max(l => l.Lead.LeadDays);
            var (from, to) = TravelReminderRules.Window(definition, maxLead, nowUtc);
            var candidates = await repository.GetCandidatesAsync(tenantId, definition.RuleType, from, to, null, cancellationToken);
            foreach (var lead in group)
            {
                foreach (var candidate in candidates.Where(c => TravelReminderRules.IsDue(definition, lead.Lead.LeadDays, c.DueAt, nowUtc)))
                {
                    result.Candidates++;
                    var staffChannels = TravelReminderRules.StaffChannels(lead.Lead);
                    if (definition.SupportsStaff && staffChannels.Count > 0)
                        await DispatchAsync(tenantId, definition, lead, candidate, "Staff", staffChannels, nowUtc, zone, result, cancellationToken);

                    var customerChannels = TravelReminderRules.CustomerChannels(lead.Lead);
                    if (definition.SupportsCustomer && customerChannels.Count > 0)
                    {
                        if (quiet) { result.Held++; continue; }
                        await DispatchAsync(tenantId, definition, lead, candidate, "Customer", customerChannels, nowUtc, zone, result, cancellationToken);
                    }
                }
            }
        }

        if (result.Sent + result.Failed + result.Skipped > 0)
        {
            await audit.RecordAsync(new AuditEventWrite(AuditEventKey, EntityType: "TravelReminders", Action: "Dispatch", Success: result.Failed == 0,
                Message: $"sent={result.Sent} skipped={result.Skipped} failed={result.Failed} held={result.Held}", TenantId: tenantId, UserId: null), cancellationToken);
        }
        return result;
    }

    private async Task DispatchAsync(int tenantId, TravelReminderDefinition definition, TravelReminderRules.EffectiveLead lead, TravelReminderCandidate candidate,
        string audience, IReadOnlyList<string> channels, DateTime nowUtc, TimeZoneInfo zone, TravelReminderRunResultDto result, CancellationToken ct)
    {
        var key = TravelReminderRules.DedupeKey(definition, lead.Lead.LeadDays, candidate, audience, nowUtc);
        var text = TravelReminderMessages.Build(definition, candidate, audience, audience == "Customer" ? lead.CustomerTemplate : null, zone);
        var claim = new TravelReminderClaim(definition.RuleType, lead.Lead.LeadDays, candidate, audience, key, channels, text.Title, text.Body);
        var id = await repository.TryClaimAsync(tenantId, claim, ct);
        if (id is not long rowId) return; // already reminded for this trigger

        // Who gets it: staff → the assigned agent (notification pipeline resolves their email); customer → contact on file.
        var usable = audience == "Staff"
            ? (candidate.StaffUserId is not null ? channels : [])
            : channels.Where(c => c == "Email" ? !string.IsNullOrWhiteSpace(candidate.CustomerEmail) : !string.IsNullOrWhiteSpace(candidate.CustomerPhone)).ToList();
        if (usable.Count == 0)
        {
            var reason = audience == "Staff" ? NoAgentReason : $"No customer {(channels.Contains("Email") && channels.Count == 1 ? "email" : "contact")} on file";
            await repository.MarkAsync(tenantId, rowId, "Skipped", null, reason, ct);
            result.Skipped++;
            return;
        }

        try
        {
            var notificationId = await notifications.CreateAndDispatchAsync(new NotificationCreateOptions(
                UserId: audience == "Staff" ? candidate.StaffUserId : null, TenantId: tenantId, Title: text.Title, Message: text.Body,
                Type: NotificationType.TravelReminder, ReferenceId: candidate.EntityId, Priority: definition.Kind == TravelReminderKind.Overdue ? 3 : 2,
                Channel: usable[0], RecipientType: audience == "Staff" ? "User" : "Customer", TemplateKey: TemplateKey, SendNow: true,
                Email: audience == "Customer" ? candidate.CustomerEmail : null, Phone: audience == "Customer" ? candidate.CustomerPhone : null,
                Module: Module, Channels: usable), ct);
            await repository.MarkAsync(tenantId, rowId, "Sent", notificationId, null, ct);
            result.Sent++;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Travel reminder {Key} failed for tenant {TenantId}", key, tenantId);
            await repository.MarkAsync(tenantId, rowId, "Failed", null, Truncate(ex.Message), ct);
            result.Failed++;
        }
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];

    public static TimeZoneInfo ResolveZone(string? id)
    {
        try { return string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}
