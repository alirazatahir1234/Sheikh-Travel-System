# SheikhGo TravelNexus — architecture

TravelNexus is the travel agency, visa and Umrah module of SheikhGo Nexus. It is a module inside the
existing API and ERP, not a separate application. This document records the repository assessment,
the decisions taken, the data model and the delivery roadmap.

Status: **Phase 1 delivered** (module foundation, configuration, customers, travelers).

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

**Later phases (planned)**

- CRM: `TravelEnquiries`, `TravelEnquiryFollowUps`, `TravelCommunicationLogs`, `TravelLostReasons`.
- Visa: `TravelVisaCases`, `TravelVisaApplicants`, `TravelVisaApplicantDocuments`, `TravelVisaStatusHistory` (append-only).
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

## 4. Security model

- **Tenant isolation.** Every statement filters on `TenantId`; joins repeat the predicate. Inserts that depend
  on a parent use `INSERT … SELECT … WHERE parent.TenantId = @TenantId`. Travel tables are listed in the SG-01
  guard (`Sg01TenantSqlGuardScanner.TenantOwnedTables`); `TravelNexusTenantGuardTests` fails the build if any
  TravelNexus statement misses the predicate.
- **Branch scope.** `ITravelBranchScopeResolver` turns the user's data scope into a branch clamp. Records outside
  the clamp are reported as not found, never as forbidden, so their existence is not revealed.
- **Permissions (Phase 1).** `Travel.View`, `Travel.Settings`, `Travel.Customer.View`, `Travel.Customer.Manage`,
  `Travel.Traveler.ViewPII`. Granted by the migration to SUPER_ADMIN, TENANT_ADMIN and OWNER. Every permission maps
  to module `TRAVEL_AGENCY`, so it is inert for companies without the module.
- **PII.** Passport and CNIC numbers are never logged (the MediatR behaviours do not serialise payloads) and are
  masked in responses without `Travel.Traveler.ViewPII`.

## 5. API (Phase 1)

All routes require the `TRAVEL_AGENCY` module. Responses use the standard `ApiResponse<T>` envelope.

| Method | Route | Permission |
|---|---|---|
| GET | `api/travel/config/countries` · `lookups` · `visa-types` · `document-types` · `lead-sources` · `settings` | Travel.View |
| POST / PUT / DELETE | `api/travel/config/visa-types[/{id}]`, `document-types[/{id}]`, `lead-sources[/{id}]`; PUT `settings` | Travel.Settings |
| GET | `api/travel/customers` (page, pageSize, search, customerKind, agentUserId, branchId) · `{customerId}` · `{customerId}/travelers` | Travel.Customer.View |
| POST / PUT / DELETE | `api/travel/customers[/{customerId}]`, POST `{customerId}/travelers` | Travel.Customer.Manage |
| GET / PUT / DELETE | `api/travel/travelers/{id}` | View / Manage |

## 6. Roadmap

| Phase | Scope | Status |
|---|---|---|
| 0 | Repository analysis and this document | Done |
| 1 | Module, permissions, navigation, configuration, customers, travelers | Done |
| 2 | Enquiries, follow-ups, conversion; visa cases, applicants, status pipeline + history | Planned |
| 3 | Checklist templates and versions, private document upload, verification, missing-document alerts, passport OCR | Planned |
| 4 | Packages, Umrah groups, pilgrims, hotels, rooms, transport / Ziyarat, manifest | Planned |
| 5 | Flight and hotel bookings, PNR / tickets, vouchers, itineraries, cancellations | Planned |
| 6 | Travel ledger, suppliers, supplier bills, commissions, profitability | Planned |
| 7 | Reminder rules and dispatch, dashboard, reports | Planned |
| 8 | Live-SQL end-to-end tests, security review, deployment notes, user guide | Planned |

Each phase gets its own plan, migration, tests and commits on the feature branch.
