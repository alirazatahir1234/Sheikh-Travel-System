# WhatsApp Testing

## Backend unit tests

Location: `Backend/SheikhTravelSystem.Tests/WhatsApp/`

| File | Coverage |
|------|----------|
| `WhatsAppWebhookTests.cs` | Verify challenge match/reject; signature HMAC |
| `WhatsAppWebhookIngestTests.cs` | Ingest parsing, idempotency paths |
| `WhatsAppOutboundRoutingTests.cs` | Phone→account routing; outbound send |
| `WhatsAppCloudApiServiceTests.cs` | Graph client; **token must not appear in errors/logs** |
| `WhatsAppAccountAdminTests.cs` | Account list/active/health; ManageAccounts |
| `WhatsAppInboxFilterAndMutationTests.cs` | Filters, assignment, status, bot |
| `WhatsAppDemoBotAndCrmTests.cs` | DEMO qualification + CRM lead + handoff |
| `WhatsAppMessagingWindowTests.cs` | 24h window open/closed edges |
| `WhatsAppMessageStatusPrecedenceTests.cs` | Forward-only status; stored `WindowExpiresAt` |
| `WhatsAppRetryAndWindowClosedTests.cs` | Retry max 3; `WINDOW_CLOSED` on retry |
| `WhatsAppTemplateSendTests.cs` | Draft rejected; Approved send; text → `WINDOW_CLOSED` |
| `WhatsAppAutomationUnitTests.cs` | Dedupe, quiet hours, haversine, proximity, ETA clamp, token hash, rating payloads |
| `WhatsAppSelfServiceBotTests.cs` | Phase 3 menu, track/invoice ownership, booking FSM, pay link, Flow idempotency |

### Commands

```bash
cd Backend
dotnet test SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj --filter FullyQualifiedName~WhatsApp
dotnet build SheikhTravelSystem.API/SheikhTravelSystem.API.csproj -c Release
```

WhatsApp tests use **mocks** for Graph (`IWhatsAppCloudApiService`) and repositories. They do **not** call the live Meta API.

Phase 2 SIT checklist: [PHASE2.md](./PHASE2.md).  
Phase 3 SIT checklist: [PHASE3.md](./PHASE3.md).

## Acceptance contracts (Phase 1)

| AC | Expectation |
|----|-------------|
| Bad signature | `POST /api/whatsapp/webhook` → **401**; log `Rejected`; not enqueued |
| Fast ACK | Valid signature → log + enqueue → **200** without waiting for ingest |
| Free-text outside window | **409** body `code: WINDOW_CLOSED` |
| Status ticks | Queued→Sent→Delivered→Read only (ignore regressions) |
| Retry | Max 3 attempts via `POST /api/whatsapp/messages/{id}/retry` |
| Duplicate wamid | Unique index + handler skip → 0 duplicates |
| Template sync | `POST .../accounts/{id}/templates/sync` upserts APPROVED/etc. |

## Frontend

| File | Coverage |
|------|----------|
| `whatsapp-inbox/whatsapp-window.spec.ts` | Badge open / amber (&lt;1h) / closed |

```bash
cd Frontend/sheikhgo-erp
npx ng build --configuration production
# Unit tests (requires Chrome / CHROME_BIN):
npx ng test --watch=false --browsers=ChromeHeadless --include='**/whatsapp-window.spec.ts'
```

Manual UI checks: composer disabled when closed; countdown badge colours; failed message Retry; context panel Link/Create lead; Templates → Sync from Meta; Webhook logs → Requeue.

## SIT (Meta test number)

Remain manual: send from Meta test number, confirm delivered/read ticks, template outside window, no Meta 131047 from free-text outside window.
