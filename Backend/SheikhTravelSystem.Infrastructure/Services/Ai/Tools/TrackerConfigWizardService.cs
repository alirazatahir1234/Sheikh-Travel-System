using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers.Commands;

namespace SheikhTravelSystem.Infrastructure.Services.Ai.Tools;

/// <summary>
/// Step-by-step tracker registration: one question per turn, then CONFIRM to write.
/// Draft lives in AiPendingActions (ToolName = ConfigureTracker).
/// </summary>
public sealed class TrackerConfigWizardService(
    IDbConnectionFactory dbFactory,
    IMediator mediator,
    ILogger<TrackerConfigWizardService> logger) : ITrackerConfigWizardService
{
    public const string ToolName = "ConfigureTracker";
    /// <summary>In-progress draft — not shown as a Confirm bar action.</summary>
    public const string DraftToolName = "ConfigureTracker.Draft";
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] StartTriggers =
    [
        "register tracker", "configure tracker", "add tracker", "setup tracker",
        "set up tracker", "register device", "configure device", "add gps",
        "register imei", "configure imei", "new tracker", "tracker registration",
        "tracker configure", "config tracker"
    ];

    public async Task ClearAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM AiPendingActions
            WHERE SessionId = @SessionId
              AND ToolName IN (@ToolName, @DraftToolName)
            """,
            new { SessionId = sessionId, ToolName, DraftToolName },
            cancellationToken: cancellationToken));
    }

    public async Task<TrackerConfigWizardTurn?> TryHandleAsync(
        Guid sessionId,
        int tenantId,
        int userId,
        string message,
        TrackerConfigWizardPermissions permissions,
        CancellationToken cancellationToken = default)
    {
        var text = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        await PurgeExpiredAsync(cancellationToken);

        var existing = await LoadStateAsync(sessionId, tenantId, userId, cancellationToken);
        var isConfirm = IsConfirmMessage(text);
        var isCancel = IsCancelMessage(text);
        var imeiInMessage = ExtractImei(text);
        // Only auto-start on a bare/near-bare IMEI so unrelated questions with 15 digits don't open the wizard.
        var wantsStart = LooksLikeStart(text)
            || (imeiInMessage is not null && existing is null && IsImeiFocusedMessage(text, imeiInMessage));

        if (existing is null && !wantsStart && !isConfirm)
            return null;

        if (!permissions.CanExecuteWrite || !permissions.CanManageDevices)
        {
            return new TrackerConfigWizardTurn(
                "I can walk you through tracker setup, but you need both **Ai.ExecuteWrite** and **Gps.DeviceManage** permissions. Ask an admin to grant those, then try again.",
                "tracker_wizard_denied",
                false,
                null);
        }

        if (isCancel && existing is not null)
        {
            await ClearAsync(sessionId, cancellationToken);
            return new TrackerConfigWizardTurn(
                "Cancelled tracker configuration. Nothing was registered.",
                "tracker_wizard_cancelled",
                false,
                null);
        }

        if (isConfirm)
        {
            if (existing is null || existing.Phase != WizardPhase.Ready)
            {
                if (existing is not null)
                {
                    return new TrackerConfigWizardTurn(
                        "We still need a few details before registering. " + PromptForStep(existing),
                        "tracker_wizard_collecting",
                        false,
                        await SaveStateAsync(sessionId, tenantId, userId, existing, cancellationToken));
                }

                return null;
            }

            return await ExecuteRegisterAsync(sessionId, existing, cancellationToken);
        }

        WizardState state;
        if (existing is null)
        {
            if (imeiInMessage is not null)
            {
                state = new WizardState
                {
                    Phase = WizardPhase.Collecting,
                    Step = WizardStep.Name,
                    Draft = new TrackerDraft { UniqueId = imeiInMessage }
                };
                var pending = await SaveStateAsync(sessionId, tenantId, userId, state, cancellationToken);
                return new TrackerConfigWizardTurn(
                    $"Got IMEI **{imeiInMessage}**. Let's configure this tracker one step at a time.\n\n" +
                    PromptForStep(state) +
                    "\n\n_Reply **skip** for optional fields, **cancel** to stop, or **done** after required fields to jump to review._",
                    "tracker_wizard_collecting",
                    false,
                    pending);
            }

            state = new WizardState
            {
                Phase = WizardPhase.Collecting,
                Step = WizardStep.Imei,
                Draft = new TrackerDraft()
            };
            var startPending = await SaveStateAsync(sessionId, tenantId, userId, state, cancellationToken);
            return new TrackerConfigWizardTurn(
                "I'll register and configure this tracker on the server step by step — one question at a time.\n\n" +
                PromptForStep(state) +
                "\n\n_Reply **skip** for optional fields, **cancel** to stop, or **done** after required fields to jump to review._",
                "tracker_wizard_collecting",
                false,
                startPending);
        }

        if (existing.Phase == WizardPhase.Ready)
        {
            // User sent something other than CONFIRM while ready — treat as edit request or re-prompt.
            if (LooksLikeStart(text) || imeiInMessage is not null)
            {
                await ClearAsync(sessionId, cancellationToken);
                return await TryHandleAsync(sessionId, tenantId, userId, message, permissions, cancellationToken);
            }

            var readyPending = await SaveStateAsync(sessionId, tenantId, userId, existing, cancellationToken);
            return new TrackerConfigWizardTurn(
                BuildReadySummary(existing) + "\n\nReply **CONFIRM** to register, or **cancel** to abort.",
                "tracker_wizard_ready",
                true,
                readyPending);
        }

        // Collecting: apply answer to current step
        var apply = ApplyAnswer(existing, text);
        if (!apply.Ok)
        {
            var retryPending = await SaveStateAsync(sessionId, tenantId, userId, existing, cancellationToken);
            return new TrackerConfigWizardTurn(
                apply.Error + "\n\n" + PromptForStep(existing),
                "tracker_wizard_collecting",
                false,
                retryPending);
        }

        var next = Advance(apply.State!);
        if (next.Phase == WizardPhase.Ready)
        {
            var confirmPending = await SaveStateAsync(sessionId, tenantId, userId, next, cancellationToken);
            return new TrackerConfigWizardTurn(
                BuildReadySummary(next) + "\n\nReply **CONFIRM** (or tap Confirm) to register this tracker on the server. Reply **cancel** to abort.",
                "tracker_wizard_ready",
                true,
                confirmPending);
        }

        var nextPending = await SaveStateAsync(sessionId, tenantId, userId, next, cancellationToken);
        var ack = apply.Ack is null ? "" : apply.Ack + "\n\n";
        return new TrackerConfigWizardTurn(
            ack + PromptForStep(next),
            "tracker_wizard_collecting",
            false,
            nextPending);
    }

    private async Task<TrackerConfigWizardTurn> ExecuteRegisterAsync(
        Guid sessionId, WizardState state, CancellationToken cancellationToken)
    {
        var d = state.Draft;
        if (string.IsNullOrWhiteSpace(d.UniqueId) || string.IsNullOrWhiteSpace(d.Name) ||
            string.IsNullOrWhiteSpace(d.Category) || string.IsNullOrWhiteSpace(d.TrackerModelKey))
        {
            await ClearAsync(sessionId, cancellationToken);
            return new TrackerConfigWizardTurn(
                "Some required details are missing. Please start again with the IMEI or say **register tracker**.",
                "tracker_wizard_incomplete",
                false,
                null);
        }

        var dto = new RegisterTrackerDto(
            Name: d.Name.Trim(),
            UniqueId: d.UniqueId,
            Category: d.Category.Trim().ToLowerInvariant(),
            TrackerModelId: 0,
            TrackerModelKey: d.TrackerModelKey,
            Phone: NullIfBlank(d.Phone),
            Contact: NullIfBlank(d.Contact),
            SosNumber: NullIfBlank(d.SosNumber),
            CountryCode: NullIfBlank(d.CountryCode),
            SIMProvider: NullIfBlank(d.SimProvider),
            SerialNumber: NullIfBlank(d.SerialNumber),
            Vendor: NullIfBlank(d.Vendor),
            SupportsEngineCutoff: d.SupportsEngineCutoff,
            RelayOutput: d.SupportsEngineCutoff ? (NullIfBlank(d.RelayOutput) ?? "out1") : null,
            RelayPurpose: d.SupportsEngineCutoff ? "engine" : null,
            CurrentStatus: "Available");

        try
        {
            var response = await mediator.Send(new RegisterTrackerCommand(dto), cancellationToken);
            await ClearAsync(sessionId, cancellationToken);

            if (!response.Success || response.Data is null)
            {
                return new TrackerConfigWizardTurn(
                    $"Could not register tracker: {response.Message ?? "Unknown error"}. You can fix the details and say **register tracker** to try again.",
                    "tracker_wizard_failed",
                    false,
                    null);
            }

            var r = response.Data;
            var sb = new StringBuilder();
            sb.AppendLine($"✓ Tracker registered: **{r.Name}** (IMEI {r.UniqueId}).");
            sb.AppendLine($"• Protocol: {r.ProtocolLabel}");
            if (r.TraccarDeviceId is int tid)
                sb.AppendLine($"• Traccar device id: {tid}");
            if (!string.IsNullOrWhiteSpace(r.StatusMessage))
                sb.AppendLine($"• {r.StatusMessage}");
            sb.AppendLine();
            sb.AppendLine("Server-side registration is done. Device firmware settings (APN / server IP via SMS) still need the GPS Commands screen if the unit is not already pointed at your Traccar server.");

            return new TrackerConfigWizardTurn(sb.ToString().Trim(), "tracker_wizard_done", false, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ConfigureTracker register failed for IMEI {Imei}", d.UniqueId);
            await ClearAsync(sessionId, cancellationToken);
            return new TrackerConfigWizardTurn(
                $"Registration failed: {ex.Message}",
                "tracker_wizard_failed",
                false,
                null);
        }
    }

    private static (bool Ok, string? Error, string? Ack, WizardState? State) ApplyAnswer(WizardState state, string text)
    {
        var draft = state.Draft;
        var skip = IsSkip(text);
        var done = IsDone(text);
        var raw = text.Trim();

        if (done && HasRequired(draft))
        {
            var ready = state with { Phase = WizardPhase.Ready, Step = WizardStep.Done };
            return (true, null, "Skipping remaining optional fields.", ready);
        }

        if (done && !HasRequired(draft))
        {
            return (false, "I still need the required fields (IMEI, name, category, model) before we can finish.", null, null);
        }

        switch (state.Step)
        {
            case WizardStep.Imei:
            {
                var imei = ExtractImei(raw) ?? (Regex.IsMatch(raw, @"^\d{15}$") ? raw : null);
                if (imei is null)
                    return (false, "Please send a valid **15-digit IMEI** (digits only).", null, null);
                return (true, null, $"IMEI set to **{imei}**.",
                    state with { Draft = draft with { UniqueId = imei } });
            }
            case WizardStep.Name:
            {
                if (skip || raw.Length < 2)
                    return (false, "Tracker **name** is required (2–100 characters).", null, null);
                if (raw.Length > 100)
                    return (false, "Name must be at most 100 characters.", null, null);
                return (true, null, $"Name set to **{raw}**.",
                    state with { Draft = draft with { Name = raw } });
            }
            case WizardStep.Category:
            {
                var cat = NormalizeCategory(raw);
                if (cat is null)
                    return (false,
                        "Pick a category: **truck**, **car**, **bus**, **motorcycle**, **trailer**, or **asset**.",
                        null, null);
                return (true, null, $"Category set to **{cat}**.",
                    state with { Draft = draft with { Category = cat } });
            }
            case WizardStep.Model:
            {
                var key = ResolveModelKey(raw);
                if (key is null)
                    return (false, "Pick a model key from the list (for example `jimi_gt06` or `teltonika_fmb920`).", null, null);
                var info = TrackerCatalog.Resolve(key)!;
                return (true, null, $"Model set to **{info.Label}** (`{key}`).",
                    state with
                    {
                        Draft = draft with
                        {
                            TrackerModelKey = key,
                            SupportsEngineCutoff = info.SupportsEngineCutoff,
                            RelayOutput = info.SupportsEngineCutoff ? (draft.RelayOutput ?? "out1") : draft.RelayOutput
                        }
                    });
            }
            case WizardStep.Country:
            {
                if (skip)
                    return (true, null, "Skipped country.", state with { Draft = draft with { CountryCode = null } });
                var code = NormalizeCountry(raw);
                if (code is null)
                    return (false, "Send a country dial code like **+92**, **+971**, **+966**, or reply **skip**.", null, null);
                return (true, null, $"Country set to **{code}**.",
                    state with { Draft = draft with { CountryCode = code } });
            }
            case WizardStep.Phone:
            {
                if (skip)
                    return (true, null, "Skipped SIM phone.", state with { Draft = draft with { Phone = null } });
                return (true, null, $"SIM phone set to **{raw}**.",
                    state with { Draft = draft with { Phone = raw } });
            }
            case WizardStep.Contact:
            {
                if (skip)
                    return (true, null, "Skipped contact.", state with { Draft = draft with { Contact = null } });
                return (true, null, $"Contact set to **{raw}**.",
                    state with { Draft = draft with { Contact = raw } });
            }
            case WizardStep.Sos:
            {
                if (skip)
                    return (true, null, "Skipped SOS number.", state with { Draft = draft with { SosNumber = null } });
                return (true, null, $"SOS number set to **{raw}**.",
                    state with { Draft = draft with { SosNumber = raw } });
            }
            case WizardStep.SimProvider:
            {
                if (skip)
                    return (true, null, "Skipped SIM provider.", state with { Draft = draft with { SimProvider = null } });
                return (true, null, $"SIM provider set to **{raw}**.",
                    state with { Draft = draft with { SimProvider = raw } });
            }
            case WizardStep.Serial:
            {
                if (skip)
                    return (true, null, "Skipped serial number.", state with { Draft = draft with { SerialNumber = null } });
                return (true, null, $"Serial set to **{raw}**.",
                    state with { Draft = draft with { SerialNumber = raw } });
            }
            case WizardStep.Vendor:
            {
                if (skip)
                    return (true, null, "Skipped vendor.", state with { Draft = draft with { Vendor = null } });
                return (true, null, $"Vendor set to **{raw}**.",
                    state with { Draft = draft with { Vendor = raw } });
            }
            case WizardStep.Cutoff:
            {
                if (skip)
                {
                    return (true, null, "Skipped engine immobilizer.",
                        state with { Draft = draft with { SupportsEngineCutoff = false, RelayOutput = null } });
                }
                if (IsYes(raw))
                {
                    return (true, null, "Engine immobilizer **enabled** (relay out1).",
                        state with
                        {
                            Draft = draft with
                            {
                                SupportsEngineCutoff = true,
                                RelayOutput = draft.RelayOutput ?? "out1"
                            }
                        });
                }
                if (IsNo(raw))
                {
                    return (true, null, "Engine immobilizer **disabled**.",
                        state with { Draft = draft with { SupportsEngineCutoff = false, RelayOutput = null } });
                }
                return (false, "Reply **yes**, **no**, or **skip**.", null, null);
            }
            default:
                return (false, "Unexpected step — say **cancel** and start again.", null, null);
        }
    }

    private static WizardState Advance(WizardState state)
    {
        if (state.Phase == WizardPhase.Ready)
            return state;

        var nextStep = state.Step switch
        {
            WizardStep.Imei => WizardStep.Name,
            WizardStep.Name => WizardStep.Category,
            WizardStep.Category => WizardStep.Model,
            WizardStep.Model => WizardStep.Country,
            WizardStep.Country => WizardStep.Phone,
            WizardStep.Phone => WizardStep.Contact,
            WizardStep.Contact => WizardStep.Sos,
            WizardStep.Sos => WizardStep.SimProvider,
            WizardStep.SimProvider => WizardStep.Serial,
            WizardStep.Serial => WizardStep.Vendor,
            WizardStep.Vendor => WizardStep.Cutoff,
            WizardStep.Cutoff => WizardStep.Done,
            _ => WizardStep.Done
        };

        if (nextStep == WizardStep.Done)
            return state with { Phase = WizardPhase.Ready, Step = WizardStep.Done };

        return state with { Step = nextStep };
    }

    private static string PromptForStep(WizardState state) => state.Step switch
    {
        WizardStep.Imei => "**1/12 — IMEI:** What is the tracker’s 15-digit IMEI?",
        WizardStep.Name => "**2/12 — Name:** What display name should we use? (e.g. `Truck-12 GPS`)",
        WizardStep.Category => "**3/12 — Category:** truck, car, bus, motorcycle, trailer, or asset?",
        WizardStep.Model => "**4/12 — Model:** Pick one:\n" + FormatModelList(),
        WizardStep.Country => "**5/12 — Country (optional):** Dial code like `+92` / `+971`, or **skip**.",
        WizardStep.Phone => "**6/12 — SIM phone (optional):** Full number on the tracker SIM, or **skip**.",
        WizardStep.Contact => "**7/12 — Contact (optional):** Owner/contact name or number, or **skip**.",
        WizardStep.Sos => "**8/12 — SOS number (optional):** Human SOS / control-center number (not the tracker SIM), or **skip**.",
        WizardStep.SimProvider => "**9/12 — SIM provider (optional):** e.g. Jazz, Zong, Etisalat — or **skip**.",
        WizardStep.Serial => "**10/12 — Serial number (optional):** Device serial, or **skip**.",
        WizardStep.Vendor => "**11/12 — Vendor (optional):** Supplier/vendor name, or **skip**.",
        WizardStep.Cutoff => "**12/12 — Engine immobilizer (optional):** Enable cutoff relay? Reply **yes**, **no**, or **skip**.",
        _ => "Review the details and reply **CONFIRM** to register."
    };

    private static string FormatModelList()
    {
        var sb = new StringBuilder();
        foreach (var (key, info) in TrackerCatalog.Models.OrderBy(kv => kv.Value.Vendor).ThenBy(kv => kv.Value.Label))
            sb.AppendLine($"• `{key}` — {info.Label}");
        return sb.ToString().TrimEnd();
    }

    private static string BuildReadySummary(WizardState state)
    {
        var d = state.Draft;
        var model = TrackerCatalog.Resolve(d.TrackerModelKey)?.Label ?? d.TrackerModelKey;
        var sb = new StringBuilder();
        sb.AppendLine("Here is the tracker configuration ready to register on the server:");
        sb.AppendLine($"• **IMEI:** {d.UniqueId}");
        sb.AppendLine($"• **Name:** {d.Name}");
        sb.AppendLine($"• **Category:** {d.Category}");
        sb.AppendLine($"• **Model:** {model} (`{d.TrackerModelKey}`)");
        if (!string.IsNullOrWhiteSpace(d.CountryCode)) sb.AppendLine($"• **Country:** {d.CountryCode}");
        if (!string.IsNullOrWhiteSpace(d.Phone)) sb.AppendLine($"• **SIM phone:** {d.Phone}");
        if (!string.IsNullOrWhiteSpace(d.Contact)) sb.AppendLine($"• **Contact:** {d.Contact}");
        if (!string.IsNullOrWhiteSpace(d.SosNumber)) sb.AppendLine($"• **SOS:** {d.SosNumber}");
        if (!string.IsNullOrWhiteSpace(d.SimProvider)) sb.AppendLine($"• **SIM provider:** {d.SimProvider}");
        if (!string.IsNullOrWhiteSpace(d.SerialNumber)) sb.AppendLine($"• **Serial:** {d.SerialNumber}");
        if (!string.IsNullOrWhiteSpace(d.Vendor)) sb.AppendLine($"• **Vendor:** {d.Vendor}");
        sb.AppendLine($"• **Engine immobilizer:** {(d.SupportsEngineCutoff ? "yes" : "no")}");
        return sb.ToString().TrimEnd();
    }

    private async Task<WizardState?> LoadStateAsync(
        Guid sessionId, int tenantId, int userId, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PendingRow>(new CommandDefinition("""
            SELECT TOP 1 p.ToolName, p.ArgsJson, p.Summary
            FROM AiPendingActions p
            INNER JOIN AiChatSessions s ON s.Id = p.SessionId
            WHERE p.SessionId = @SessionId
              AND p.ToolName IN (@ToolName, @DraftToolName)
              AND s.TenantId = @TenantId AND s.UserId = @UserId AND s.IsDeleted = 0
              AND p.ExpiresAt > GETUTCDATE()
            ORDER BY CASE WHEN p.ToolName = @ToolName THEN 0 ELSE 1 END
            """,
            new { SessionId = sessionId, ToolName, DraftToolName, TenantId = tenantId, UserId = userId },
            cancellationToken: cancellationToken));

        if (row is null || string.IsNullOrWhiteSpace(row.ArgsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<WizardState>(row.ArgsJson, JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to parse ConfigureTracker wizard state");
            return null;
        }
    }

    private async Task<AiPendingActionDto> SaveStateAsync(
        Guid sessionId, int tenantId, int userId, WizardState state,
        CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var id = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.Add(PendingTtl);
        var argsJson = JsonSerializer.Serialize(state, JsonOpts);
        var ready = state.Phase == WizardPhase.Ready;
        var summary = ready ? BuildReadySummary(state) : PromptForStep(state);
        var storedToolName = ready ? ToolName : DraftToolName;

        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM AiPendingActions
            WHERE SessionId = @SessionId
              AND ToolName IN (@ToolName, @DraftToolName);

            INSERT INTO AiPendingActions (Id, SessionId, TenantId, UserId, ToolName, ArgsJson, Summary, CreatedAt, ExpiresAt)
            VALUES (@Id, @SessionId, @TenantId, @UserId, @StoredToolName, @ArgsJson, @Summary, GETUTCDATE(), @ExpiresAt);
            """,
            new
            {
                Id = id,
                SessionId = sessionId,
                TenantId = tenantId,
                UserId = userId,
                ToolName,
                DraftToolName,
                StoredToolName = storedToolName,
                ArgsJson = argsJson,
                Summary = summary,
                ExpiresAt = expiresAt
            },
            cancellationToken: cancellationToken));

        return new AiPendingActionDto(storedToolName, summary, expiresAt);
    }

    private async Task PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AiPendingActions WHERE ExpiresAt <= GETUTCDATE()",
            cancellationToken: cancellationToken));
    }

    private sealed record PendingRow(string ToolName, string ArgsJson, string Summary);

    private static bool LooksLikeStart(string text)
    {
        var q = text.ToLowerInvariant();
        return StartTriggers.Any(t => q.Contains(t, StringComparison.Ordinal));
    }

    private static bool IsConfirmMessage(string message)
        => Regex.IsMatch(message.Trim(), @"^CONFIRM(\s|$)", RegexOptions.IgnoreCase);

    private static bool IsCancelMessage(string message)
        => Regex.IsMatch(message.Trim(), @"^(cancel|abort|stop)(\s+tracker)?$", RegexOptions.IgnoreCase);

    private static bool IsSkip(string text)
        => Regex.IsMatch(text.Trim(), @"^(skip|none|n/?a|-)$", RegexOptions.IgnoreCase);

    private static bool IsDone(string text)
        => Regex.IsMatch(text.Trim(), @"^(done|finish|review|that's all|thats all)$", RegexOptions.IgnoreCase);

    private static bool IsYes(string text)
        => Regex.IsMatch(text.Trim(), @"^(y|yes|true|enable|on)$", RegexOptions.IgnoreCase);

    private static bool IsNo(string text)
        => Regex.IsMatch(text.Trim(), @"^(n|no|false|disable|off)$", RegexOptions.IgnoreCase);

    private static string? ExtractImei(string text)
    {
        var m = Regex.Match(text, @"\b(\d{15})\b");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static bool IsImeiFocusedMessage(string text, string imei)
    {
        var rest = text.Replace(imei, " ", StringComparison.Ordinal)
            .ToLowerInvariant();
        rest = Regex.Replace(rest, @"[^a-z\s]", " ");
        rest = Regex.Replace(rest, @"\s+", " ").Trim();
        if (string.IsNullOrEmpty(rest))
            return true;

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "register", "configure", "config", "setup", "set", "up", "add", "new",
            "tracker", "device", "gps", "imei", "please", "plz", "for", "this", "my", "the", "a", "an"
        };
        return rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(allowed.Contains);
    }

    private static string? NormalizeCategory(string raw)
    {
        var t = raw.Trim().ToLowerInvariant().Replace(' ', '_');
        if (t is "bike" or "motorbike") t = "motorcycle";
        if (t is "lorry") t = "truck";
        return TrackerCatalog.ValidCategories.Contains(t) ? t : null;
    }

    private static string? ResolveModelKey(string raw)
    {
        var t = raw.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        if (TrackerCatalog.Models.ContainsKey(t))
            return t;

        // Allow friendly labels: "Jimi GT06", "Teltonika FMB920"
        foreach (var (key, info) in TrackerCatalog.Models)
        {
            if (info.Label.Equals(raw.Trim(), StringComparison.OrdinalIgnoreCase))
                return key;
            if (key.Replace("_", "").Equals(t.Replace("_", ""), StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return null;
    }

    private static string? NormalizeCountry(string raw)
    {
        var t = raw.Trim().Replace(" ", "");
        if (!t.StartsWith('+'))
            t = "+" + t.TrimStart('0');
        return Regex.IsMatch(t, @"^\+\d{1,4}$") ? t : null;
    }

    private static bool HasRequired(TrackerDraft d)
        => !string.IsNullOrWhiteSpace(d.UniqueId)
           && !string.IsNullOrWhiteSpace(d.Name)
           && !string.IsNullOrWhiteSpace(d.Category)
           && !string.IsNullOrWhiteSpace(d.TrackerModelKey);

    private static WizardStep FirstMissingRequired(TrackerDraft d)
    {
        if (string.IsNullOrWhiteSpace(d.UniqueId)) return WizardStep.Imei;
        if (string.IsNullOrWhiteSpace(d.Name)) return WizardStep.Name;
        if (string.IsNullOrWhiteSpace(d.Category)) return WizardStep.Category;
        if (string.IsNullOrWhiteSpace(d.TrackerModelKey)) return WizardStep.Model;
        return WizardStep.Country;
    }

    private static string? NullIfBlank(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private enum WizardPhase
    {
        Collecting,
        Ready
    }

    private enum WizardStep
    {
        Imei,
        Name,
        Category,
        Model,
        Country,
        Phone,
        Contact,
        Sos,
        SimProvider,
        Serial,
        Vendor,
        Cutoff,
        Done
    }

    private sealed record WizardState
    {
        public WizardPhase Phase { get; init; } = WizardPhase.Collecting;
        public WizardStep Step { get; init; } = WizardStep.Imei;
        public TrackerDraft Draft { get; init; } = new();
    }

    private sealed record TrackerDraft
    {
        public string? UniqueId { get; init; }
        public string? Name { get; init; }
        public string? Category { get; init; }
        public string? TrackerModelKey { get; init; }
        public string? CountryCode { get; init; }
        public string? Phone { get; init; }
        public string? Contact { get; init; }
        public string? SosNumber { get; init; }
        public string? SimProvider { get; init; }
        public string? SerialNumber { get; init; }
        public string? Vendor { get; init; }
        public bool SupportsEngineCutoff { get; init; }
        public string? RelayOutput { get; init; }
    }
}

/// <summary>
/// Final write tool — used when chat Confirm button executes a ready ConfigureTracker pending action.
/// </summary>
public sealed class ConfigureTrackerTool(
    IMediator mediator,
    ILogger<ConfigureTrackerTool> logger) : IAiTool
{
    public string Name => TrackerConfigWizardService.ToolName;
    public string Description =>
        "Registers a GPS tracker on the server from a completed step-by-step wizard (IMEI + details). Requires confirmation.";
    public string Kind => "write";
    public bool RequiresConfirmation => true;
    public IReadOnlyList<string> TriggerKeywords { get; } =
    [
        "register tracker", "configure tracker", "add tracker", "setup tracker", "register imei"
    ];

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AiToolExecutionContext context, JsonElement? args, CancellationToken cancellationToken = default)
    {
        // Keyword path: nudge user into the conversational wizard (needs a session).
        if (!context.ConfirmWrite)
        {
            return new AiToolExecutionResult(
                Name,
                true,
                "I can register this tracker step by step. Send the **15-digit IMEI**, or say **register tracker** and I will ask for each detail one at a time.",
                args,
                PendingConfirmation: false);
        }

        // Confirm path: args should be wizard state JSON from AiPendingActions.
        if (args is not { ValueKind: JsonValueKind.Object })
        {
            return new AiToolExecutionResult(Name, false, string.Empty,
                Error: "Missing tracker configuration draft. Start again with the IMEI.");
        }

        string? imei = null;
        string? name = null;
        string? category = null;
        string? modelKey = null;
        string? phone = null;
        string? contact = null;
        string? sos = null;
        string? country = null;
        string? simProvider = null;
        string? serial = null;
        string? vendor = null;
        var cutoff = false;
        string? relay = null;

        if (args.Value.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.Object)
        {
            imei = GetStr(draft, "uniqueId");
            name = GetStr(draft, "name");
            category = GetStr(draft, "category");
            modelKey = GetStr(draft, "trackerModelKey");
            phone = GetStr(draft, "phone");
            contact = GetStr(draft, "contact");
            sos = GetStr(draft, "sosNumber");
            country = GetStr(draft, "countryCode");
            simProvider = GetStr(draft, "simProvider");
            serial = GetStr(draft, "serialNumber");
            vendor = GetStr(draft, "vendor");
            if (draft.TryGetProperty("supportsEngineCutoff", out var c) &&
                (c.ValueKind == JsonValueKind.True || c.ValueKind == JsonValueKind.False))
                cutoff = c.GetBoolean();
            relay = GetStr(draft, "relayOutput");
        }

        if (string.IsNullOrWhiteSpace(imei) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(modelKey))
        {
            return new AiToolExecutionResult(Name, false, string.Empty,
                Error: "Incomplete tracker draft (need IMEI, name, category, model).");
        }

        var dto = new RegisterTrackerDto(
            Name: name.Trim(),
            UniqueId: imei.Trim(),
            Category: category.Trim().ToLowerInvariant(),
            TrackerModelId: 0,
            TrackerModelKey: modelKey.Trim(),
            Phone: phone,
            Contact: contact,
            SosNumber: sos,
            CountryCode: country,
            SIMProvider: simProvider,
            SerialNumber: serial,
            Vendor: vendor,
            SupportsEngineCutoff: cutoff,
            RelayOutput: cutoff ? (relay ?? "out1") : null,
            RelayPurpose: cutoff ? "engine" : null,
            CurrentStatus: "Available");

        try
        {
            var response = await mediator.Send(new RegisterTrackerCommand(dto), cancellationToken);
            if (!response.Success || response.Data is null)
            {
                return new AiToolExecutionResult(Name, false, string.Empty,
                    Error: response.Message ?? "Registration failed.");
            }

            var r = response.Data;
            return new AiToolExecutionResult(
                Name, true,
                $"Registered **{r.Name}** (IMEI {r.UniqueId}). Traccar id: {r.TraccarDeviceId?.ToString() ?? "—"}. {r.StatusMessage}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ConfigureTrackerTool failed");
            return new AiToolExecutionResult(Name, false, string.Empty, Error: ex.Message);
        }
    }

    private static string? GetStr(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}
