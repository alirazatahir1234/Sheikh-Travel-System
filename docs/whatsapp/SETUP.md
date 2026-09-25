# WhatsApp Setup

Do **not** put Meta access tokens, App Secret, or verify tokens in committed `appsettings.json` (ADR-009). Bind secrets via Railway environment variables or `dotnet user-secrets`.

`WhatsApp:Enabled` defaults to `false` in committed config (`"WhatsApp": { "Enabled": false }` only).

## Railway environment variables

Set these on the API service via Railway’s UI or CLI. **Never commit the values.**

Required names:

- `WhatsApp__Enabled` (use `true`)
- `WhatsApp__WebhookVerifyToken` (alias: `WhatsApp__VerifyToken`)
- `WhatsApp__AppSecret`
- `WhatsApp__GraphApiVersion` (example: `v21.0`)
- `WhatsApp__MessageRetentionDays` (example: `180`)
- `WhatsApp__Uae__PhoneNumberId`
- `WhatsApp__Uae__BusinessAccountId` (optional)
- `WhatsApp__Uae__AccessToken`
- `WhatsApp__Pakistan__PhoneNumberId`
- `WhatsApp__Pakistan__BusinessAccountId` (optional)
- `WhatsApp__Pakistan__AccessToken`

Optional / legacy aliases:

- `WhatsApp__Accounts__UAE__PhoneNumberId`
- `WhatsApp__Accounts__UAE__AccessToken`
- `WhatsApp__Accounts__UAE__BusinessAccountId`
- `WhatsApp__Accounts__PK__PhoneNumberId`
- `WhatsApp__Accounts__PK__AccessToken`
- `WhatsApp__Accounts__PK__BusinessAccountId`
- `Notifications__WhatsApp__Enabled`
- `WhatsApp__NotificationTemplateName` (example: `support_update`)
- `WhatsApp__NotificationTemplateLanguage` (example: `en`)

Example local bind (replace ellipsis with your values; do not commit them):

```bash
dotnet user-secrets set "WhatsApp:Enabled" "true" --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:WebhookVerifyToken" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:AppSecret" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Uae:PhoneNumberId" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Uae:AccessToken" "..." --project SheikhTravelSystem.API
# Pakistan likewise: WhatsApp:Pakistan:PhoneNumberId / AccessToken
```

## Meta Developer Console

1. Open the Meta app with the **WhatsApp** product.
2. Ensure UAE and Pakistan business numbers are available.
3. Webhook callback URL (production example):

   `https://<your-railway-api-host>/api/whatsapp/webhook`

4. Verify token must match `WhatsApp__WebhookVerifyToken` (or `WhatsApp__VerifyToken`).
5. Subscribe to the `messages` field.
6. Copy each number’s **Phone number ID** into the matching env vars above.
7. Use a **permanent** system-user access token for production (rotate via Railway; never commit).

## Local development

```bash
cd Backend
dotnet user-secrets set "WhatsApp:Enabled" "true" --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:WebhookVerifyToken" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:AppSecret" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Uae:PhoneNumberId" "..." --project SheikhTravelSystem.API
dotnet user-secrets set "WhatsApp:Uae:AccessToken" "..." --project SheikhTravelSystem.API
# Pakistan: WhatsApp:Pakistan:PhoneNumberId / AccessToken
```

Use a public tunnel (ngrok / Cloudflare Tunnel) so Meta can reach `https://<tunnel>/api/whatsapp/webhook`.

## Database

Migrations are applied by the API startup registry (`WhatsAppInboxMigration`, `WhatsAppDomainFoundationMigration`, `WhatsAppAccountAdminMigration`, `WhatsAppCrmLeadMigration`, `WhatsAppTemplatesMigration`, …).

Templates seed as **Draft** for catalog names such as `demo_confirmation`, `support_update`, etc. Mark **Approved** in ERP only after Meta approval — never assume seed rows are sendable.

Permissions `WhatsApp.View` / `Reply` / `Manage` / `ManageAccounts` / `ManageTemplates` are seeded and granted to admin roles by migrations.

## Frontend

```bash
cd Frontend/sheikhgo-erp
npm start   # or ng serve
```

Routes (JWT + permission guards):

| Route | Notes |
|-------|--------|
| `/whatsapp` | Inbox |
| `/whatsapp/accounts` | Needs `WhatsApp.ManageAccounts` or `WhatsApp.Manage` |
| `/whatsapp/templates` | Needs `WhatsApp.ManageTemplates` or `WhatsApp.View` |

Angular never receives Meta tokens — only `hasAccessToken: boolean`.

## Smoke after setup

1. API health + login.
2. `GET /api/whatsapp/accounts` → UAE/PK with `hasAccessToken: true` when env is set.
3. Meta webhook verify returns challenge.
4. Send a test message from phone → appears in inbox.
