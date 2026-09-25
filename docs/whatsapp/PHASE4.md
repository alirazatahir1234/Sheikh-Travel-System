# WhatsApp Phase 4 — Intelligence & Growth

Phase 4 turns WhatsApp into a smarter sales/support surface **without** replacing human agents or rewriting Phases 1–3.

| Sub-phase | Status | Summary |
|-----------|--------|---------|
| **4.1 AI Agent Assist** | Shipped | Suggest / transform / summarize for agents; never auto-sends |
| **4.2 RAG + Urdu/Roman Urdu + Handover** | Planned | Approved knowledge retrieval, multilingual, confidence handover |
| **4.3 Broadcasts / Campaigns** | Planned | Consent-first templates, dry-run, queue + throttle |
| **4.4 Analytics** | Planned | Operational + sales metrics dashboard |

---

## 4.1 — AI Agent Assist

### Principle

AI **assists** agents. It does **not** send customer-facing WhatsApp messages automatically.

Flow:

```text
Agent opens conversation
  → AI Assist
  → Generate suggestion (grounded in ERP facts)
  → Review / Edit
  → Send via existing outbound path
```

### Permission

| Permission | Use |
|------------|-----|
| `WhatsApp.AiAssist` | Suggest / transform / summarize APIs and inbox panel |
| `WhatsApp.Reply` | Still required to send |

Seeded for `SUPER_ADMIN` / `TENANT_ADMIN` via `WhatsAppPhase4AiAssistMigration`.

### APIs

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/whatsapp/conversations/{id}/ai/suggest` | Optional `priorSuggestion`, `instruction` |
| POST | `/api/whatsapp/conversations/{id}/ai/transform` | `action`: Regenerate \| Shorter \| Professional \| Translate |
| POST | `/api/whatsapp/conversations/{id}/ai/summary` | Conversation summary for agents |

Response includes: `suggestion`, `confidence` (High/Medium/Low), `reviewRecommended`, `provider`, `model`, `durationMs`, `unavailableFacts`.

### Grounding

Suggestions are built from:

- Recent messages (capped)
- Existing conversation context (customer, lead, bookings, unpaid total)
- Latest **active** trip facts when available (driver first name, vehicle, plate, tracking URL)

The model is instructed **not** to invent booking refs, drivers, plates, amounts, GPS, or payment status. Missing data → ask for a reference.

### Provider

Reuses tenant `AiProviderConfig` + `IAiProvider` (Ollama live today). Does **not** route through the fleet `IAiChatGateway` chat sessions.

### Audit

`WhatsAppAiAssistLogs` stores: operation, conversation id, provider, model, duration, success, confidence, error code.  
Does **not** store full conversation text or API keys.  
Usage also recorded via `RecordUsageAsync(..., feature: "whatsapp_assist", ...)`.

### Inbox UI

Thread header **AI Assist** (when permitted): Generate, Regenerate, Make Shorter, More Professional, Translate, Summarize.  
Suggestion loads into the editable composer; **Send** uses the existing send path only.

### Tests

`WhatsAppAiAssistUnitTests` mocks `IAiProvider` — no real LLM calls.

### Non-goals (4.1)

- Auto-send / LLM bot replacing human
- RAG / embeddings (4.2)
- Full Urdu/Roman Urdu detection & smart handover (4.2)
- Campaigns (4.3), Analytics (4.4)
