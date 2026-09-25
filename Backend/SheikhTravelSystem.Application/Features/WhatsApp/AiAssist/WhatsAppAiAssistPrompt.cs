using System.Text;
using System.Text.Json;
using SheikhTravelSystem.Application.Features.WhatsApp.DTOs;

namespace SheikhTravelSystem.Application.Features.WhatsApp.AiAssist;

/// <summary>
/// Builds grounded prompts and parses structured AI assist responses.
/// Pure helpers — no LLM calls (unit-testable with mocks at the provider boundary).
/// </summary>
public static class WhatsAppAiAssistPrompt
{
    public const int MaxMessages = 30;
    public const int MaxBodyChars = 500;

    public static string SystemPromptSuggest =>
        """
        You are SheikhGo WhatsApp Agent Assist. You help human agents draft replies.
        You NEVER send messages to customers. You only suggest text for an agent to review.

        HARD RULES:
        - Use ONLY facts from the PROVIDED FACTS block. Do not invent booking references, driver names,
          vehicle plates, invoice amounts, payment status, GPS locations, or customer details.
        - If a fact is missing, say the information is unavailable and ask the customer for the relevant reference.
        - Do not claim live GPS coordinates unless they appear in FACTS.
        - Prefer concise, professional customer-facing WhatsApp tone.

        Respond with ONLY valid JSON (no markdown):
        {
          "suggestion": "the reply text for the agent to edit/send",
          "confidence": "High|Medium|Low",
          "confidenceReason": "short reason",
          "unavailableFacts": ["list of facts you needed but were missing"]
        }
        """;

    public static string SystemPromptSummary =>
        """
        You are SheikhGo WhatsApp Agent Assist. Summarize the conversation for a human agent.
        Use ONLY the PROVIDED FACTS and messages. Do not invent details.

        Respond with ONLY valid JSON (no markdown):
        {
          "suggestion": "multi-line summary covering: request, pickup/destination if mentioned, vehicle preference, booking/trip refs if known, open questions, next recommended action",
          "confidence": "High|Medium|Low",
          "confidenceReason": "short reason",
          "unavailableFacts": []
        }
        """;

    public static string SystemPromptTransform(string action, string? targetLanguage)
    {
        var langLine = string.IsNullOrWhiteSpace(targetLanguage)
            ? ""
            : $"Target language: {targetLanguage.Trim()}";
        return $$"""
            You are SheikhGo WhatsApp Agent Assist. Transform the agent's draft text.
            Action: {{action}}
            {{langLine}}
            Preserve booking references, invoice numbers, vehicle plates, person names, URLs, dates, and monetary values exactly.
            Do not invent new ERP facts.

            Respond with ONLY valid JSON (no markdown):
            {
              "suggestion": "transformed text",
              "confidence": "High|Medium|Low",
              "confidenceReason": "short reason",
              "unavailableFacts": []
            }
            """;
    }

    public static string BuildFactsBlock(
        WhatsAppConversationContextDto? context,
        WhatsAppAiActiveTripFactsDto? activeTrip,
        IReadOnlyList<WhatsAppMessageDto> messages,
        out IReadOnlyList<string> unavailableHints)
    {
        var missing = new List<string>();
        var sb = new StringBuilder();
        sb.AppendLine("=== PROVIDED FACTS (authoritative; do not invent beyond this) ===");

        if (context is null)
        {
            missing.Add("conversation_context");
            sb.AppendLine("Conversation context: UNAVAILABLE");
        }
        else
        {
            sb.AppendLine($"ContactPhone: {context.ContactPhone}");
            sb.AppendLine($"ContactName: {NullOr(context.ContactName)}");
            sb.AppendLine($"CustomerId: {NullOr(context.CustomerId?.ToString())}");
            sb.AppendLine($"CustomerName: {NullOr(context.CustomerName)}");
            sb.AppendLine($"CustomerCompany: {NullOr(context.CustomerCompany)}");
            sb.AppendLine($"LeadId: {NullOr(context.LeadId?.ToString())}");
            sb.AppendLine($"LeadStatus: {NullOr(context.LeadStatus)}");
            sb.AppendLine($"UnpaidInvoiceTotal: {context.UnpaidInvoiceTotal}");

            if (context.ActiveBookings.Count == 0)
            {
                missing.Add("active_bookings");
                sb.AppendLine("ActiveBookings: NONE");
            }
            else
            {
                sb.AppendLine("ActiveBookings:");
                foreach (var b in context.ActiveBookings)
                    sb.AppendLine($"  - {b.BookingNumber} | {b.Status} | TravelDate={NullOr(b.TravelDate?.ToString("u"))}");
            }

            if (context.LastCompletedTrip is null)
            {
                missing.Add("last_completed_trip");
                sb.AppendLine("LastCompletedTrip: NONE");
            }
            else
            {
                var t = context.LastCompletedTrip;
                sb.AppendLine($"LastCompletedTrip: {NullOr(t.TripNumber)} | {t.Status} | CompletedAt={NullOr(t.CompletedAt?.ToString("u"))}");
            }
        }

        if (activeTrip is null)
        {
            missing.Add("active_trip_driver_vehicle");
            sb.AppendLine("ActiveTrip: NONE (no driver/vehicle/tracking facts available)");
        }
        else
        {
            sb.AppendLine("ActiveTrip:");
            sb.AppendLine($"  TripNumber: {NullOr(activeTrip.TripNumber)}");
            sb.AppendLine($"  BookingNumber: {NullOr(activeTrip.BookingNumber)}");
            sb.AppendLine($"  Status: {activeTrip.StatusLabel}");
            sb.AppendLine($"  DriverFirstName: {NullOr(activeTrip.DriverFirstName)}");
            sb.AppendLine($"  VehicleDescription: {NullOr(activeTrip.VehicleDescription)}");
            sb.AppendLine($"  PlateNumber: {NullOr(activeTrip.PlateNumber)}");
            sb.AppendLine($"  TrackingUrl: {NullOr(activeTrip.TrackingUrl)}");
            if (string.IsNullOrWhiteSpace(activeTrip.DriverFirstName))
                missing.Add("driver_name");
            if (string.IsNullOrWhiteSpace(activeTrip.VehicleDescription) && string.IsNullOrWhiteSpace(activeTrip.PlateNumber))
                missing.Add("vehicle_info");
            if (string.IsNullOrWhiteSpace(activeTrip.TrackingUrl))
                missing.Add("tracking_link");
        }

        sb.AppendLine();
        sb.AppendLine("=== RECENT MESSAGES (oldest → newest) ===");
        if (messages.Count == 0)
        {
            missing.Add("messages");
            sb.AppendLine("(empty conversation)");
        }
        else
        {
            foreach (var m in messages)
            {
                var body = Truncate(m.Body);
                sb.AppendLine($"[{m.CreatedAtUtc:u}] {m.Direction}/{m.Type}: {body}");
            }
        }

        unavailableHints = missing;
        return sb.ToString();
    }

    public static string BuildSuggestUserPrompt(
        string factsBlock,
        string? priorSuggestion,
        string? instruction)
    {
        var sb = new StringBuilder();
        sb.AppendLine(factsBlock);
        sb.AppendLine();
        sb.AppendLine("Task: Draft a suggested WhatsApp reply for the agent based on the latest customer need.");
        if (!string.IsNullOrWhiteSpace(priorSuggestion))
        {
            sb.AppendLine("Avoid repeating this prior suggestion; improve or rephrase:");
            sb.AppendLine(priorSuggestion.Trim());
        }
        if (!string.IsNullOrWhiteSpace(instruction))
        {
            sb.AppendLine("Agent instruction:");
            sb.AppendLine(instruction.Trim());
        }
        return sb.ToString();
    }

    public static string BuildSummaryUserPrompt(string factsBlock)
    {
        var sb = new StringBuilder();
        sb.AppendLine(factsBlock);
        sb.AppendLine();
        sb.AppendLine("Task: Produce a concise conversation summary for a human agent takeover.");
        return sb.ToString();
    }

    public static string BuildTransformUserPrompt(string text, string factsBlock)
    {
        var sb = new StringBuilder();
        sb.AppendLine(factsBlock);
        sb.AppendLine();
        sb.AppendLine("Draft to transform:");
        sb.AppendLine(text.Trim());
        return sb.ToString();
    }

    public static WhatsAppAiParsedResponse ParseModelJson(
        string? raw,
        IReadOnlyList<string> contextUnavailable,
        bool emptyConversation)
    {
        var fallbackSuggestion =
            "I don't have enough verified information in SheikhGo to answer accurately. " +
            "Please share your booking or trip reference so our team can help.";

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new WhatsAppAiParsedResponse(
                fallbackSuggestion,
                WhatsAppAiAssistConfidence.Low,
                "Empty model response",
                contextUnavailable.ToList(),
                ParsedFromJson: false);
        }

        var json = ExtractJsonObject(raw);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var suggestion = root.TryGetProperty("suggestion", out var s) ? s.GetString()?.Trim() : null;
            var confidence = root.TryGetProperty("confidence", out var c) ? c.GetString()?.Trim() : null;
            var reason = root.TryGetProperty("confidenceReason", out var r) ? r.GetString()?.Trim() : null;
            var unavailable = new List<string>(contextUnavailable);
            if (root.TryGetProperty("unavailableFacts", out var uf) && uf.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in uf.EnumerateArray())
                {
                    var v = item.GetString();
                    if (!string.IsNullOrWhiteSpace(v) && !unavailable.Contains(v, StringComparer.OrdinalIgnoreCase))
                        unavailable.Add(v!);
                }
            }

            if (string.IsNullOrWhiteSpace(suggestion))
            {
                return new WhatsAppAiParsedResponse(
                    fallbackSuggestion,
                    WhatsAppAiAssistConfidence.Low,
                    "Model returned no suggestion",
                    unavailable,
                    ParsedFromJson: true);
            }

            var normalized = NormalizeConfidence(confidence, emptyConversation, unavailable.Count, contextUnavailable.Count);
            return new WhatsAppAiParsedResponse(
                suggestion!,
                normalized.Level,
                reason ?? normalized.Reason,
                unavailable,
                ParsedFromJson: true);
        }
        catch
        {
            // Model returned prose — treat as medium unless conversation empty.
            var level = emptyConversation || contextUnavailable.Count >= 3
                ? WhatsAppAiAssistConfidence.Low
                : WhatsAppAiAssistConfidence.Medium;
            return new WhatsAppAiParsedResponse(
                Truncate(raw, 2000) ?? fallbackSuggestion,
                level,
                "Unstructured model response",
                contextUnavailable.ToList(),
                ParsedFromJson: false);
        }
    }

    public static (string Level, string Reason) NormalizeConfidence(
        string? modelConfidence,
        bool emptyConversation,
        int unavailableCount,
        int contextMissingCount)
    {
        if (emptyConversation)
            return (WhatsAppAiAssistConfidence.Low, "Empty conversation");

        var raw = (modelConfidence ?? "").Trim();
        var level = raw.Equals("High", StringComparison.OrdinalIgnoreCase) ? WhatsAppAiAssistConfidence.High
            : raw.Equals("Medium", StringComparison.OrdinalIgnoreCase) ? WhatsAppAiAssistConfidence.Medium
            : raw.Equals("Low", StringComparison.OrdinalIgnoreCase) ? WhatsAppAiAssistConfidence.Low
            : WhatsAppAiAssistConfidence.Medium;

        // Grounding: missing critical ERP facts cannot stay High.
        if (contextMissingCount >= 2 && level == WhatsAppAiAssistConfidence.High)
            level = WhatsAppAiAssistConfidence.Medium;
        if (unavailableCount >= 3 || contextMissingCount >= 4)
            level = WhatsAppAiAssistConfidence.Low;

        var reason = level switch
        {
            WhatsAppAiAssistConfidence.High => "Sufficient grounded facts",
            WhatsAppAiAssistConfidence.Medium => "Partial context; human review advised",
            _ => "Human review recommended"
        };
        return (level, reason);
    }

    public static string NormalizeTransformAction(string? action)
    {
        var a = (action ?? "").Trim();
        if (a.Equals("Regenerate", StringComparison.OrdinalIgnoreCase))
            return WhatsAppAiAssistOperations.Regenerate;
        if (a.Equals("Shorter", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("MakeShorter", StringComparison.OrdinalIgnoreCase))
            return WhatsAppAiAssistOperations.Shorter;
        if (a.Equals("Professional", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("MakeMoreProfessional", StringComparison.OrdinalIgnoreCase))
            return WhatsAppAiAssistOperations.Professional;
        if (a.Equals("Translate", StringComparison.OrdinalIgnoreCase))
            return WhatsAppAiAssistOperations.Translate;
        return a;
    }

    private static string ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            return raw[start..(end + 1)];
        return raw;
    }

    private static string NullOr(string? v) => string.IsNullOrWhiteSpace(v) ? "UNAVAILABLE" : v.Trim();

    private static string Truncate(string? body, int max = MaxBodyChars)
    {
        if (string.IsNullOrWhiteSpace(body)) return "(no text)";
        var t = body.Trim().Replace("\r\n", "\n");
        return t.Length <= max ? t : t[..max] + "…";
    }
}

public sealed record WhatsAppAiParsedResponse(
    string Suggestion,
    string Confidence,
    string ConfidenceReason,
    IReadOnlyList<string> UnavailableFacts,
    bool ParsedFromJson);
