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
| `WhatsAppTemplateSendTests.cs` | Draft rejected; Approved send; text blocked outside window |

### Commands

```bash
cd Backend
dotnet test SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj --filter FullyQualifiedName~WhatsApp
dotnet build SheikhTravelSystem.API/SheikhTravelSystem.API.csproj -c Release
```

WhatsApp tests use **mocks** for Graph (`IWhatsAppCloudApiService`) and repositories. They do **not** call the live Meta API.

There is **no** dedicated WhatsApp integration-test project in this repo. Full suite (`dotnet test` without filter) may include unrelated ERP failures; treat WhatsApp filter results as the module gate.

## Frontend

There are currently **no** Angular `*.spec.ts` files for WhatsApp modules/services.

```bash
cd Frontend/sheikhgo-erp
npx ng build --configuration production
# Unit tests (requires Chrome / CHROME_BIN):
npx ng test --watch=false --browsers=ChromeHeadless
```

## What is mocked vs live

| Layer | Unit tests | Live Meta |
|-------|------------|-----------|
| Signature / verify helpers | Yes | Also needed on Railway |
| Ingest / bot / CRM / templates | Yes (mocked Graph) | Needs real numbers |
| Cloud API HTTP | Mocked + sanitize tests | Needs tokens |
| SignalR | Partial (publisher mocks) | Needs running API + ERP |
| Duplicate webhook under load | Logic covered; not load-tested | Manual |

## Manual / Meta checklist

See [PRODUCTION.md](PRODUCTION.md) for the 12-item production verification list.
