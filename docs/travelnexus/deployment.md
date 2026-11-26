# SheikhGo TravelNexus — deployment notes

TravelNexus is a module inside the existing API and ERP: there is nothing separate to deploy. This page lists what the
module needs from the host, what the migrations do, how a company is switched on, and how to check a deployment.
Secrets follow `Backend/README.secrets.md`: user-secrets locally, environment variables (or Key Vault) on hosts, never
`appsettings*.json`.

## 1. Configuration

| Purpose | Configuration key | Environment variable | Default / notes |
|---|---|---|---|
| Private document store (Azure) | `TravelDocuments:AzureContainerName` | `TravelDocuments__AzureContainerName` | `travel-documents`. Private container, no anonymous access, no SAS is ever issued. Used when `FileStorage:Provider` is `Azure`. |
| Private document store (local) | `TravelDocuments:LocalRoot` | `TravelDocuments__LocalRoot` | `private-storage/travel-documents`, relative to the content root. Must stay outside `FileStorage:RootPath` (`uploads`), which is served publicly. |
| Storage provider | `FileStorage:Provider`, `FileStorage:AzureConnectionString` | `FileStorage__Provider`, `FileStorage__AzureConnectionString` | `Azure` or `Local`. The Azure connection string is a secret. |
| Reminder scheduler | `TravelNexus:Reminders:Enabled` | `TravelNexus__Reminders__Enabled` | `true`. Set `false` on a host that must not send (e.g. a second API instance, a staging copy of production data). |
| Reminder interval | `TravelNexus:Reminders:IntervalMinutes` | `TravelNexus__Reminders__IntervalMinutes` | `30` (clamped). First pass 60 s after start. One instance should run the scheduler. |
| Passport OCR (opt-in) | `Ocr:AzureEndpoint`, `Ocr:AzureApiKey`, `Ocr:AzureTimeoutSeconds`, `Ocr:ConfidenceThreshold`, `Ocr:EnableFallback` | `Ocr__AzureEndpoint`, `Ocr__AzureApiKey`, … | Without endpoint and key the scan endpoint is inert. The key is a secret. Passport images leave the platform to Azure Document Intelligence (see `security-review.md` TN-11). |
| Name transliteration (opt-in) | `Translator:Key`, `Translator:Region`, `Translator:Endpoint` | `Translator__Key`, … | Azure Translator; key is a secret. |
| Tenant default time zone | `Localization:DefaultTimeZone` | `Localization__DefaultTimeZone` | `Asia/Karachi`. Reminder due dates and quiet hours use the tenant's time zone, falling back to this. |
| Email provider | `Notifications:Email:*` | `Notifications__Email__*` | Shared with the platform; customer reminders by email need it. |
| SMS provider | `Notifications:Sms:*` (`Http:BaseUrl`, `DefaultFrom`, …) | `Notifications__Sms__*` | Shared with the platform. |
| WhatsApp | `Notifications:WhatsApp:Enabled`, `WhatsApp:NotificationTemplateName`, `WhatsApp:Language` | `Notifications__WhatsApp__Enabled`, `WhatsApp__NotificationTemplateName`, `WhatsApp__Language` | Customer reminders outside the 24-hour window need the tenant's approved Meta template name (default `support_update`, `en`). |
| Rate limits | `Program.cs` policy `general` | — | Document upload and passport scan: 60 requests/minute per IP (no limit in Development). nginx adds 120 requests/minute and a 12 MB body cap on `/api/travel/(documents|passport)/`. |

Everything else TravelNexus needs (connection string, JWT) is the platform's.

## 2. Database

Migrations run at API start-up (`DatabaseMigrationRunner`), in this order, all additive and idempotent:

1. `TravelNexusFoundationMigration` — module, permissions, menus, settings, countries, customers, travelers
2. `TravelNexusCrmVisaMigration` — enquiries, visa cases, applicants, history
3. `TravelNexusDocumentsMigration` — checklists, document requirements and files
4. `TravelNexusUmrahMigration` — hotels, packages, groups, rooming, transport
5. `TravelNexusBookingsMigration` — suppliers, booking files, flights, hotel bookings, cancellations, airports
6. `TravelNexusFinanceMigration` — invoices, receipts, allocations, credit notes, refunds
7. `TravelNexusSupplierFinanceMigration` — rate cards, bills, supplier payments, commissions, exchange rates
8. `TravelNexusRemindersMigration` — reminder rules and log, quiet hours, notification templates, Phase 7 permissions and menus
9. `TravelNexusModuleGrantBackfillMigration` — grants Travel permissions to the admin roles of companies that already have the module

Each migration grants its permission codes to `SUPER_ADMIN`, `TENANT_ADMIN` and `OWNER` of the tenants that exist when
it runs; companies enabled later are covered by the grant hook (section 3). Proof that the chain applies on an empty
database: `scripts/ci/travelnexus-live-sql-e2e.sh` (section 5).

Rollback: there is no down-migration. The tables are additive and inert for companies without the module; to take the
module away from a company, disable `TRAVEL_AGENCY` for it (its menu and endpoints disappear, data stays).

## 3. Enabling TravelNexus for a company

1. Platform → Companies → the company → Modules → tick **TravelNexus** (`TRAVEL_AGENCY`) and save
   (`PUT api/platform/tenants/{id}/modules`). Provisioning a new company with the module ticked does the same.
2. The grant hook adds all `Travel.*` permissions to the company's `TENANT_ADMIN` and `OWNER` roles at that moment
   (never removed on disable). Other roles get Travel permissions through role management as usual.
3. Users already signed in see the Travel menu after their next token refresh (at most 60 minutes) or a new sign-in.
4. In the ERP: Travel → Settings → set the default and reporting currency, number prefixes, passport-expiry warning
   days and reminder quiet hours; add visa types, document types, lead sources, hotels, suppliers and exchange rates.

## 4. Storage

| Store | What | Access |
|---|---|---|
| `travel-documents` (Azure) or `private-storage/travel-documents` (local) | Applicant documents (passport scans, photos, forms) | Private. Streamed only by `GET api/travel/documents/files/{id}` with `Travel.Document.Download`, audited fail-closed, `no-store`. |
| SQL | Everything else, including SHA-256 of every file | Tenant-filtered. |

Back the private store up with the database; a file row without its blob is reported as missing in the UI.

## 5. Verification after a deployment

1. API log shows the nine TravelNexus migrations as applied or already applied, none failed.
2. Sign in as a company with the module: `GET api/travel/settings` returns 200; a company without the module gets 403.
3. Travel → Dashboard loads; every card names its window.
4. Upload a 1-page PDF to a document requirement and open it: the audit log shows `travel.document.download`.
5. Reminders → Run now: the log fills (or shows Skipped with a reason); the bell shows the staff notification.
6. In Production configuration, call `POST api/travel/passport/scan` more than 60 times in a minute from one address:
   the 61st returns 429.
7. CI: the Phase 8 job runs `scripts/ci/travelnexus-live-sql-e2e.sh`, which builds a throwaway database from
   `Database/CompleteSchema.sql` plus all migrations and drives the three brief workflows for two companies. Locally:

   ```bash
   SHEIKHGO_TRAVELNEXUS_SQL_CONNECTION="Server=127.0.0.1,1433;Database=master;User Id=sa;Password=<throwaway>;TrustServerCertificate=True;Encrypt=False;" \
     dotnet test Backend/SheikhTravelSystem.Tests --filter "FullyQualifiedName~TravelNexus.LiveSql"
   ```

   The fixture refuses a connection string that names anything but `master`, creates `SheikhGo_TravelNexus_E2E_<stamp>`
   and drops it afterwards (`SHEIKHGO_TRAVELNEXUS_KEEP_DB=1` keeps it). Never point it at an application database.

## 6. Operations

- **Reminders** send through the platform's notification pipeline; failures are in Travel → Reminders → Log with the
  provider reason, and in the dashboard's "Reminders failed · last 7 days". Re-running never duplicates a reminder
  (dedupe key per rule, entity, due date, lead and audience).
- **Quiet hours** hold customer messages until the tenant's local morning; staff messages are not held.
- **Scaling out**: run the reminder scheduler on one instance (`TravelNexus:Reminders:Enabled=false` on the others).
- **Secrets found in the checkout** (`appsettings.Local.json`, `docker-compose.db.yml`, `docker-compose.db.env`) must be
  rotated and moved to user-secrets or host environment; see `security-review.md` TN-09.
