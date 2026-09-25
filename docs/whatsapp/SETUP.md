# WhatsApp Setup

Do **not** put Meta access tokens, App Secret, or verify tokens in committed `appsettings.json` (ADR-009). Bind secrets via Railway environment variables or `dotnet user-secrets`.

`WhatsApp:Enabled` defaults to `false` in committed config (`"WhatsApp": { "Enabled": false }` only).

**SheikhGo currently uses a single Pakistan WhatsApp Business line.** Configure only `WhatsApp__Pakistan__*` (or `WhatsApp:Pakistan:*`). UAE account rows are deactivated by migration; do not set `WhatsApp__Uae__*` unless you re-enable UAE later.

## Railway environment variables

Set these on the API service via Railway’s UI or CLI. **Never commit the values.**

Required names:

- `WhatsApp__Enabled` (use `true`)
- `WhatsApp__WebhookVerifyToken` (alias: `WhatsApp__VerifyToken`)
- `WhatsApp__AppSecret`
- `WhatsApp__GraphApiVersion` (example: `v21.0`)
- `WhatsApp__MessageRetentionDays` (example: `180`)
- `WhatsApp__Pakistan__PhoneNumberId`
- `WhatsApp__Pakistan__BusinessAccountId` (optional)
- `WhatsApp__Pakistan__AccessToken`

Optional:

- `WhatsApp__Accounts__PK__PhoneNumberId` / `AccessToken` / `BusinessAccountId` (legacy aliases)
- `WhatsApp__Routing__DefaultAccountCode` (default `PK`)
- `Notifications__WhatsApp__Enabled`
- `WhatsApp__NotificationTemplateName` (example: `support_update`)
- `WhatsApp__NotificationTemplateLanguage` (example: `en`)

Example local bind (replace ellipsis with your values; do not commit them):

```bash
dotnet user-secrets set "WhatsApp:Enabled" "true" --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:WebhookVerifyToken" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:AppSecret" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Pakistan:PhoneNumberId" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Pakistan:BusinessAccountId" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Pakistan:AccessToken" "..." --project SheikhTravelSystem.API
```

## Meta Developer Console

1. Open the Meta app with the **WhatsApp** product.
2. Note **Phone number ID** and **WhatsApp Business Account ID** for the Pakistan number.
3. Create a permanent (or long-lived) **access token** with WhatsApp permissions.
4. Configure the webhook callback URL to your public API `/api/whatsapp/webhook` (or the path documented for your deploy).
5. Set the verify token to match `WhatsApp__WebhookVerifyToken`.
6. Subscribe to message webhooks for the Pakistan phone number.

## Local development

1. Set user-secrets as above (Pakistan only).
2. Run the API; confirm startup applies WhatsApp migrations without errors.
3. ERP → WhatsApp → Accounts: Pakistan should show **active / default** and **Has token** when AccessToken is set. UAE should be inactive.
4. Use ngrok (or similar) for Meta webhook callbacks to localhost.

## ERP permissions

Grant roles as needed: `WhatsApp.View`, `WhatsApp.Reply`, `WhatsApp.Manage`, `WhatsApp.ManageAccounts`, `WhatsApp.ManageTemplates`.

## Security notes

Angular never receives Meta tokens — only `hasAccessToken: boolean`.

## Smoke checks

1. Webhook verify (GET) returns the challenge when the verify token matches.
2. `GET /api/whatsapp/accounts` → Pakistan active/default with token present when env is set; UAE inactive.
3. Send a test outbound from inbox within the 24h window (or use an Approved template).
4. Outside window: free-text returns **409 WINDOW_CLOSED**; template send still works.
5. ERP → Templates → **Sync from Meta** (requires `BusinessAccountId` + ManageTemplates).
6. ERP → WhatsApp Webhooks: list logs; requeue a Failed row after fixing config.
7. Phase 2: see [PHASE2.md](./PHASE2.md) — leave automation rules disabled until SIT; enable BookingConfirmed + DriverAssigned first.
8. Phase 3: see [PHASE3.md](./PHASE3.md). Optional: `WhatsApp__SelfServiceDefaultRouteId`, `WhatsApp__SelfServiceDefaultAmount`; enable `PortalPaymentGateway` for Pay Now.
