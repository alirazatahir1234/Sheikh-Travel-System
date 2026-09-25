# WhatsApp Production

## Production declaration

**WhatsApp is not declared production-ready until Railway secrets are set and the manual Meta checklist below has passed.**

Local unit tests and Release builds validate implementation safety; they do **not** replace live Graph / webhook verification.

## Railway checklist

| Variable | Required | Notes |
|----------|----------|--------|
| `WhatsApp__Enabled` | Yes | `true` |
| `WhatsApp__WebhookVerifyToken` | Yes | Matches Meta console |
| `WhatsApp__AppSecret` | Yes | Signature fail-closed if empty |
| `WhatsApp__Uae__PhoneNumberId` | Yes | |
| `WhatsApp__Uae__AccessToken` | Yes | Permanent token; rotate in Railway |
| `WhatsApp__Uae__BusinessAccountId` | Recommended | |
| `WhatsApp__Pakistan__PhoneNumberId` | Yes | |
| `WhatsApp__Pakistan__AccessToken` | Yes | |
| `WhatsApp__Pakistan__BusinessAccountId` | Recommended | |
| `WhatsApp__GraphApiVersion` | Recommended | e.g. `v21.0` |
| `WhatsApp__MessageRetentionDays` | Optional | Default 180 |

Never commit real values. Never put WhatsApp secrets in `appsettings.json`.

## Security expectations (production)

- Meta tokens stay server-side; Angular sees `hasAccessToken` only.
- Webhook: verify challenge + HMAC-SHA256; rate-limited (`public`).
- API errors/logs sanitize bearer tokens (`WhatsAppCloudApiService.Sanitize`).
- Inbox APIs JWT + permissions; conversations/messages filtered by `TenantId`.
- Inbound `MessageId` idempotent (check + unique index).

### Host ADR-009 note (non-WhatsApp)

Keep host configuration (database, JWT, third-party integrations) out of WhatsApp PR diffs. Bind via environment / user-secrets. Do not stage `appsettings.json` when working on WhatsApp.

## Ops notes

| Topic | Guidance |
|-------|----------|
| Retention | `WhatsAppRetentionHostedService` respects `MessageRetentionDays` |
| SignalR | Ensure reverse proxy supports WebSockets to `/hubs/whatsapp` |
| Webhook latency | Ingest + DEMO bot + Graph run synchronously — monitor Meta retries / timeouts |
| Notification channel | Outside 24h window requires Approved `NotificationTemplateName` or channel fails (no fake success) |
| Templates | Seeded Draft only; approve in ERP after Meta approval |

## Manual production checklist

1. **UAE outbound test** — agent text (inside window) or Approved template from ERP UAE account.
2. **Pakistan outbound test** — same for PK account.
3. **UAE inbound test** — customer message to UAE number lands in inbox under UAE.
4. **Pakistan inbound test** — customer message to PK number lands under PK.
5. **Webhook verification** — Meta console verify succeeds against production URL.
6. **Message persistence** — inbound + outbound rows in DB with correct account/conversation.
7. **Duplicate webhook test** — resend same payload; no duplicate message row (same Meta `MessageId`).
8. **Delivery status test** — sent → delivered → read (or failed) updates in thread.
9. **DEMO qualification test** — new lead conversation completes Idle→…→Completed (or handoff).
10. **CRM lead creation** — `WebsiteContactRequests` / conversation `LeadId` linked for WhatsApp source.
11. **Human handoff** — assign agent or agent send disables bot (`HandedOff`).
12. **Production security verification** — no tokens in FE network payloads; Railway vars set; AppSecret + verify token live; host secrets not relied on from committed appsettings for WhatsApp.

## Readiness tiers (maintain honestly)

### IMPLEMENTED

Inbox, multi-account routing, webhook verify + signature, outbound text, templates + 24h window gate, DEMO bot + CRM lead, handoff, SignalR, permissions, migrations, ERP inbox/accounts/templates, unit tests.

### VERIFIED LOCALLY (2026-09-25 review)

| Check | Result |
|-------|--------|
| Secret scan for committed Meta Graph tokens | Pass — none found |
| WhatsApp in `appsettings.json` | Pass — `"Enabled": false` only |
| Angular env WhatsApp tokens | Pass — none |
| Docker / CI WhatsApp secrets | Pass — none |
| `dotnet build` API `-c Release` | Pass |
| `dotnet test` filter `~WhatsApp` | Pass — 98 tests |
| Full `dotnet test` suite | Fail — 9 non-WhatsApp failures (unrelated ERP) |
| Dedicated WhatsApp integration tests | N/A — none in repo |
| `ng build --configuration production` | Pass |
| `ng test` ChromeHeadless | Not run — Chrome binary missing on review machine |

### VERIFIED AGAINST REAL META API

**None** in this review (no live Graph/webhook calls executed).

### NOT YET VERIFIED

UAE/PK live outbound & inbound, production webhook challenge, delivery receipts, duplicate webhook under Meta retries, DEMO/CRM on real traffic, webhook sync latency under load, host ADR-009 secret rotation for SQL/JWT/Traccar/Maps in committed appsettings.
