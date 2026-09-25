# WhatsApp Cloud API (Meta) — SheikhGo

Production-oriented docs: [docs/whatsapp/](../../docs/whatsapp/) (`ARCHITECTURE`, `SETUP`, `TESTING`, `PRODUCTION`).

Multi-number WhatsApp Business inbox. **Current deploy uses Pakistan only:**

| Code | Number | Purpose | Status |
|------|--------|---------|--------|
| PK | +923177368305 | Support / Pakistan | Active default |
| UAE | +971557701219 | Reserved / inactive | Deactivated until a UAE WABA is configured |

## Architecture

- **Webhook**: `GET/POST /api/whatsapp/webhook` (anonymous, rate-limited, `X-Hub-Signature-256`)
- **Inbox API**: `/api/whatsapp/*` (JWT + `WhatsApp.View` / `Reply` / `Manage`)
- **Realtime**: SignalR hub `/hubs/whatsapp` event `ReceiveWhatsAppEvent`
- **ERP UI**: `/whatsapp` lazy Angular module
- Tokens stay on the backend (Railway / user-secrets). Angular never calls Graph.

## Railway / user-secrets

Do **not** add WhatsApp credentials (or even empty token placeholders) to committed `appsettings.json` — staging that file fails ADR-009 scans. Bind via env / `dotnet user-secrets` only. `WhatsAppOptions` defaults keep the feature off until configured. Country→account routing defaults to **PK** in `WhatsAppRoutingOptions` (code); override with `WhatsApp__Routing__*` env if needed — do not put them in `appsettings.json`.

Set (never commit real values) via Railway or user-secrets. Variable **names** only:

| Variable | Notes |
|----------|--------|
| `WhatsApp__Enabled` | `true` when live |
| `WhatsApp__WebhookVerifyToken` | Alias: `WhatsApp__VerifyToken` |
| `WhatsApp__AppSecret` | Signature fail-closed if empty |
| `WhatsApp__GraphApiVersion` | e.g. `v21.0` |
| `WhatsApp__MessageRetentionDays` | e.g. `180` |
| `WhatsApp__Pakistan__PhoneNumberId` | PK line |
| `WhatsApp__Pakistan__BusinessAccountId` | Optional |
| `WhatsApp__Pakistan__AccessToken` | PK Graph token |

Legacy alias: `WhatsApp__Accounts__PK__*` for PhoneNumberId / AccessToken / BusinessAccountId. Do not set `WhatsApp__Uae__*` unless re-enabling UAE.

```bash
dotnet user-secrets set "WhatsApp:Enabled" "true" --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:WebhookVerifyToken" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:AppSecret" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Pakistan:PhoneNumberId" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Pakistan:BusinessAccountId" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Pakistan:AccessToken" "..." --project SheikhTravelSystem.API
```
Access tokens never leave the API — Angular only sees `HasAccessToken` (boolean).

Optional legacy gate for notification-channel sends: `Notifications__WhatsApp__Enabled`.

## Meta Developer Console

1. Create / open Meta app with **WhatsApp** product.
2. Add both business phone numbers (UAE + PK).
3. Webhook callback URL (production):

   `https://sheikh-travel-system-production.up.railway.app/api/whatsapp/webhook`

4. Verify token must match `WhatsApp__VerifyToken`.
5. Subscribe to `messages` field.
6. Copy each number’s **Phone number ID** into the corresponding account env vars.

## Local development

```bash
dotnet user-secrets set "WhatsApp:Enabled" "true" --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:VerifyToken" "dev-verify" --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:AppSecret" "..." --project SheikhTravelSystem.API
# … account PhoneNumberId + AccessToken
```

Use a public tunnel (ngrok, Cloudflare Tunnel) for Meta webhooks against local API.

## Permissions

- `WhatsApp.View` — inbox
- `WhatsApp.Reply` — send replies
- `WhatsApp.Manage` — link customers

Seeded for `SUPER_ADMIN` / `TENANT_ADMIN` by `WhatsAppInboxMigration`.

## CRM leads + DEMO bot

Inbound WhatsApp reuses **`WebsiteContactRequests`** (no separate Lead table):

1. Match `Customers` / existing contact requests by normalized phone.
2. Create a `Source=WhatsApp` contact request only when none exists.
3. Link `WhatsAppConversations.CustomerId` / `LeadId`.

**DEMO qualification** (deterministic, no AI) runs when `IsBotEnabled=true` (default on new conversations):

| Customer | Bot |
|----------|-----|
| `DEMO` | Ask fleet type → size → challenge |
| Valid option / `1`…`n` / list reply | Advance state; store on lead |
| Final answer | Thank-you; lead `Status=Qualified` |

**Human handoff:** assign agent or agent outbound reply → `IsBotEnabled=false`, `AssignedUserId` set. Re-enable bot → `CurrentBotState=Idle`.

## Media

Authenticated proxy:

`GET /api/whatsapp/media?mediaId=...&accountId=...`

Uses the account’s access token server-side; never expose the token to the browser.

## Retention

`WhatsAppRetentionHostedService` deletes messages older than `MessageRetentionDays` (default 180).
