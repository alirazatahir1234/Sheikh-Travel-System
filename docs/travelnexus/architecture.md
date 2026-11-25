# SheikhGo TravelNexus — architecture

TravelNexus is the travel agency, visa and Umrah module of SheikhGo Nexus. It is a module inside the
existing API and ERP, not a separate application. This document records the repository assessment,
the decisions taken, the data model and the delivery roadmap.

Status: **Phase 2 delivered** (Phase 1 foundation, configuration, customers, travelers; Phase 2 CRM enquiries and visa case pipeline).

## 1. Repository assessment (Phase 0)

| Area | What exists | How TravelNexus uses it |
|---|---|---|
| Runtime | .NET 10, Clean Architecture (Domain / Application / Infrastructure / API), MediatR 11, FluentValidation, Dapper only | Same stack; no new packages |
| Migrations | Idempotent C# classes in `Infrastructure/Persistence/Migrations`, ordered in `DatabaseMigrationRegistry`, applied at API start-up (`Database:RunMigrationsOnStartup`, see SG-29) | One or more migrations per phase, guarded DDL |
| Tenancy | `ITenantContext.GetRequiredTenantId()` from the JWT; manual `TenantId = @TenantId` in every statement | Every repository method takes `tenantId` and filters on it, including joins |
| Branches | `Branches` + `IDataScopeEngine` (company / branch / department scope) | Customers carry `BranchId`; branch-scoped users see their branches plus unassigned records |
| Authorization | Permission constants → policy registration + `PermissionRegistrySeed` + migration rows; `[RequirePermission]`, `[RequireModule]` | `TravelPermissions`, module `TRAVEL_AGENCY` |
| Audit | `IAuditableCommand` → `AuditLoggingBehavior` → `IAuditEngine` (request payloads are not logged) | Every write command is auditable |
| Files | `IFileStorageService` (Azure Blob / local) | Phase 3: private container, authorized download only |
| OCR | `IIdentityOcrService` (Azure Form Recognizer) | Phase 3: passport scan |
| Notifications | `INotificationService` + Email / SMS / WhatsApp / FCM senders; reminder hosted services | Phase 7 reminders |
| PDF | None on the backend; ERP has jspdf / autotable | Vouchers, invoices, manifests generated in the ERP |
| Finance | `CustomerInvoices` tied to fleet bookings, no currency; no general ledger | Separate travel ledger (Phase 6) |
| ERP | Angular 20, Material + Tailwind, Chart.js; standalone feature pattern in `features/organization-hierarchy`; UI kit in `shared/components/ui` | `features/travel-nexus`, lazy at `/travel` |
| Menu | Backend `PlatformModules` / `PlatformMenus`, fallback `core/navigation/menu-config.ts` | Rows seeded by each phase's migration + fallback group |
| Flutter app | Fleet mobile app | Not affected: the TravelNexus API is additive |

Conventions in the code that TravelNexus deliberately does **not** copy:

- PKR defaults on money columns — every TravelNexus amount carries an ISO 4217 `CurrencyCode`.
- `PaymentStatus.PartiallyPaid` on payments — travel payments use `Pending | Completed | Failed | Refunded`;
  part-payment is an invoice balance state.
- Role-name checks in `PermissionAuthorizationHandler` — TravelNexus checks permissions only.
- `BaseEntity.TenantId = 1` default — TravelNexus always sets `TenantId` from `ITenantContext`.

## 2. Decisions

| Decision | Choice | Reason |
|---|---|---|
| Base branch | `feature/Travel-Agency-Module` built on `fix/SG-01-compliance-tenant-isolation` | SG-01 tenant fixes and the SQL guard apply from day one |
| Module code | `TRAVEL_AGENCY` (legacy key `travel-agency`) | `TRAVEL` already means Routes |
| Enablement | Opt-in per company; **no automatic backfill** | `TenantType` was back-filled to "Travel Agency" for fleet tenants too, so it cannot be used to decide |
| Customers | Reuse `dbo.Customers` as the billing party + 1:1 `TravelCustomerProfiles` | No duplicated customers; fleet data untouched |
| Travelers | `TravelTravelers` (passport holders) belong to a customer | A family or company has many passport holders |
| Finance | New travel ledger (`TravelInvoices`, payments, allocations, credit notes, refunds) | Fleet invoices are booking-bound and currency-less; open SG-02/03/14 |
| Countries | Platform-scope `TravelCountries` (ISO 3166-1 alpha-2) | Shared reference data, read-only |
| PII | Passport and CNIC masked in API responses unless `Travel.Traveler.ViewPII` | Masked values are never placed in editable fields; null or masked on update keeps the stored value |

## 3. Data model

All tables are tenant-owned (`TenantId` + FK to `Tenants`) unless marked platform. Audit columns:
`CreatedAt`, `CreatedBy`, `UpdatedAt`, `IsDeleted` (soft delete). Status values are `NVARCHAR` with
CHECK constraints; money is `DECIMAL(18,2)` + `CurrencyCode CHAR(3)`.

**Phase 1 (built)**

| Table | Purpose | Key constraints |
|---|---|---|
| `TravelCountries` (platform) | ISO country list | PK `Code` |
| `TravelSettings` | One row per tenant: default currency, numbering prefixes, passport warning days | PK `TenantId` |
| `TravelVisaTypes` | Visa products per country | unique (tenant, country, name); fee requires currency |
| `TravelDocumentTypes` | Documents collected from applicants | unique (tenant, code) |
| `TravelLeadSources` | Lead sources | unique (tenant, name) |
| `TravelCustomerProfiles` | Travel data for a `Customers` row: kind, company, agent, branch, lead source | unique (tenant, customer); corporate requires company name |
| `TravelTravelers` | Passport holders of a customer, with mahram link | unique (tenant, issuing country, passport no.); expiry after issue; not own mahram |

**Phase 2 (built)**

| Table | Purpose | Key constraints |
|---|---|---|
| `TravelNumberSequences` | Per-tenant, per-year counters (`ENQ-2026`, `VC-2026`) | PK (tenant, key) |
| `TravelLostReasons` | Why enquiries are lost | unique (tenant, name) |
| `TravelEnquiries` | Leads / enquiries; lead fields or linked customer | lead name+phone or customer; Lost needs reason; Won needs customer; budget needs currency |
| `TravelEnquiryActivities` | Follow-ups and communication history (append-only, includes system StatusChange rows) | type / direction CHECKs |
| `TravelVisaCases` | One case per customer, country and visa type | status CHECK; OnHold ⇔ OnHoldFromStatus; Cancelled needs reason; submitted statuses need SubmittedAt; fees need currency |
| `TravelVisaApplicants` | Travelers on a case, each with its own decision and visa details | unique (case, traveler); Rejected needs reason |
| `TravelVisaStatusHistory` | Append-only case and applicant events | event type CHECK |

**Later phases (planned)**

- Visa documents: `TravelVisaApplicantDocuments`.
- Checklists: `TravelChecklistTemplates` (versioned), `TravelChecklistTemplateItems`.
- Umrah: `TravelPackages`, `TravelPackagePrices`, `TravelGroups`, `TravelGroupMembers`, `TravelGroupHotels`,
  `TravelRooms`, `TravelRoomAssignments`, `TravelGroupTransport`, `TravelGroupStatusHistory`.
- Bookings: `TravelFlightBookings`, `TravelFlightSegments`, `TravelFlightPassengers`, `TravelHotelBookings`,
  `TravelItineraries`, `TravelItineraryItems`, `TravelCancellations`.
- Finance: `TravelInvoices`, `TravelInvoiceLines`, `TravelInstallmentPlans`, `TravelPayments` (idempotency key),
  `TravelPaymentAllocations`, `TravelCreditNotes`, `TravelRefunds`.
- Suppliers: `TravelSuppliers`, `TravelSupplierRates`, `TravelSupplierBills`, `TravelSupplierPayments`,
  `TravelCommissionRules`, `TravelCommissions`; the supplier ledger is a view, not a copy.
- Notifications: `TravelReminderRules`, `TravelReminders`.

Every status change writes a history row in the same transaction as the status update.

## 4. Workflows (Phase 2)

**Numbering.** `TravelNumberAllocator` increments `TravelNumberSequences` under `UPDLOCK, HOLDLOCK` inside the
transaction that inserts the document: numbers are unique, gap-free for committed rows and restart each year
(`{prefix}-{yyyy}-{00001}`, prefixes from `TravelSettings`).

**Enquiry state machine** (`EnquiryStateMachine`):

```
New → Contacted → Qualified → Quoted
any open → Lost (reason required)      Lost → Contacted (Reopen)
any open → Won   (only through Convert)
```

Logging an outbound activity on a New enquiry moves it to Contacted. Conversion runs in one transaction: link an
existing customer or create one from the lead fields, optionally open a Draft visa case, set Won, write an activity.

**Visa case state machine** (`VisaCaseStateMachine`):

```
Draft → DocumentsPending → DocumentsVerified → Submitted → InProcessing
              ↑______ReopenDocuments_____↓
any open ↔ OnHold (Resume returns to the held status)     any open → Cancelled (reason)
Approved / PartiallyApproved / Rejected: derived when every non-withdrawn applicant is decided
```

Applicants change only before submission and must be travelers of the case's customer. Decisions are recorded only
in Submitted / InProcessing and need `Travel.Visa.Decide`. Every status-dependent write checks the expected status
(409 on a concurrent change) and writes history in the same transaction. The API returns `allowedActions`, so the
ERP never duplicates the rules.

**KPIs.** Pipeline and enquiry counters come from `/summary` endpoints, zero-filled for every status, with
`Total` = sum of buckets (tested). Labels state the window ("current, all time").

## 5. Security model

- **Tenant isolation.** Every statement filters on `TenantId`; joins repeat the predicate. Inserts that depend
  on a parent use `INSERT … SELECT … WHERE parent.TenantId = @TenantId`. Travel tables are listed in the SG-01
  guard (`Sg01TenantSqlGuardScanner.TenantOwnedTables`); `TravelNexusTenantGuardTests` fails the build if any
  TravelNexus statement misses the predicate.
- **Branch scope.** `ITravelBranchScopeResolver` turns the user's data scope into a branch clamp. Records outside
  the clamp are reported as not found, never as forbidden, so their existence is not revealed.
- **Permissions.** Phase 1: `Travel.View`, `Travel.Settings`, `Travel.Customer.View`, `Travel.Customer.Manage`,
  `Travel.Traveler.ViewPII`. Phase 2: `Travel.Enquiry.View/Manage`, `Travel.Visa.View/Manage`, `Travel.Visa.Decide`.
  Each phase's migration grants its own codes to SUPER_ADMIN, TENANT_ADMIN and OWNER of the tenants that exist when it
  runs. Every permission maps to module `TRAVEL_AGENCY`, so it is inert for companies without the module.
  **Known gap:** companies provisioned later get no Travel permissions until an admin grants them in role management
  (the same applies to RENTAL); a grant-on-module-enable hook is planned for Phase 8.
- **PII.** Passport and CNIC numbers are never logged (the MediatR behaviours do not serialise payloads) and are
  masked in responses without `Travel.Traveler.ViewPII`.

## 6. API

All routes require the `TRAVEL_AGENCY` module. Responses use the standard `ApiResponse<T>` envelope.

| Method | Route | Permission |
|---|---|---|
| GET | `api/travel/config/countries` · `lookups` · `visa-types` · `document-types` · `lead-sources` · `settings` | Travel.View |
| POST / PUT / DELETE | `api/travel/config/visa-types[/{id}]`, `document-types[/{id}]`, `lead-sources[/{id}]`; PUT `settings` | Travel.Settings |
| GET | `api/travel/customers` (page, pageSize, search, customerKind, agentUserId, branchId) · `{customerId}` · `{customerId}/travelers` | Travel.Customer.View |
| POST / PUT / DELETE | `api/travel/customers[/{customerId}]`, POST `{customerId}/travelers` | Travel.Customer.Manage |
| GET / PUT / DELETE | `api/travel/travelers/{id}` | View / Manage |
| GET / POST / PUT / DELETE | `api/travel/config/lost-reasons[/{id}]` | View / Settings |
| GET | `api/travel/enquiries` (search, status, serviceType, agentUserId, branchId, customerId, createdFrom/To, followUpDue) · `summary` · `by-customer/{id}` · `{id}` | Travel.Enquiry.View |
| POST / PUT | `api/travel/enquiries`, `{id}`, `{id}/status`, `{id}/lost`, `{id}/activities`, `{id}/convert` | Travel.Enquiry.Manage |
| GET | `api/travel/visa-cases` (search, status, countryCode, visaTypeId, agentUserId, customerId, createdFrom/To) · `summary` · `by-customer/{id}` · `{id}` | Travel.Visa.View |
| POST / PUT / DELETE | `api/travel/visa-cases`, `{id}`, `{id}/transition`, `{id}/submit`, `{id}/applicants[/{applicantId}]` | Travel.Visa.Manage |
| POST | `api/travel/visa-cases/{id}/applicants/{applicantId}/decision` | Travel.Visa.Decide |

## 7. Roadmap

| Phase | Scope | Status |
|---|---|---|
| 0 | Repository analysis and this document | Done |
| 1 | Module, permissions, navigation, configuration, customers, travelers | Done |
| 2 | Enquiries, follow-ups, conversion; visa cases, applicants, status pipeline + history | Done |
| 3 | Checklist templates and versions, private document upload, verification, missing-document alerts, passport OCR | Planned |
| 4 | Packages, Umrah groups, pilgrims, hotels, rooms, transport / Ziyarat, manifest | Planned |
| 5 | Flight and hotel bookings, PNR / tickets, vouchers, itineraries, cancellations | Planned |
| 6 | Travel ledger, suppliers, supplier bills, commissions, profitability | Planned |
| 7 | Reminder rules and dispatch, dashboard, reports | Planned |
| 8 | Live-SQL end-to-end tests, security review, deployment notes, user guide | Planned |

Each phase gets its own plan, migration, tests and commits on the feature branch.
