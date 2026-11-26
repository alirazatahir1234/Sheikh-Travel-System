# SheikhGo TravelNexus — architecture

TravelNexus is the travel agency, visa and Umrah module of SheikhGo Nexus. It is a module inside the
existing API and ERP, not a separate application. This document records the repository assessment,
the decisions taken, the data model and the delivery roadmap.

Status: **Phase 5 delivered** (foundation, customers, travelers; CRM enquiries and visa pipeline; document checklists, private documents, verification gate, passport MRZ; hotels, packages, Umrah groups, rooming, transport, manifest; suppliers, booking files, flights and tickets, hotel bookings, vouchers, itineraries, cancellations and refund tracking; customer invoices, receipts, installments, credit notes, refunds, balances and statements — Phase 6A).

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
| `TravelVisaCases` | One case per customer (or, from Phase 4, per Umrah group), country and visa type | status CHECK; OnHold ⇔ OnHoldFromStatus; Cancelled needs reason; submitted statuses need SubmittedAt; fees need currency |
| `TravelVisaApplicants` | Travelers on a case, each with its own decision and visa details | unique (case, traveler); Rejected needs reason |
| `TravelVisaStatusHistory` | Append-only case and applicant events | event type CHECK |

**Phase 3 (built)**

| Table | Purpose | Key constraints |
|---|---|---|
| `TravelChecklistTemplates` | Versioned checklist per country (+ optional visa type) | Draft / Active / Retired; one Active per (tenant, country, visa type); Active and Retired are read-only |
| `TravelChecklistItems` | Document type, mandatory, applies-to condition, minimum validity after travel | unique (template, document type, condition) |
| `TravelApplicantDocuments` | Per-applicant requirement snapshotted from the checklist or requested ad hoc | Missing / Uploaded / Verified / Rejected / Waived; reject and waive need a note; unique (applicant, document type) |
| `TravelDocumentFiles` | Every uploaded version: storage key, original name, sniffed content type, size, SHA-256 | content type CHECK (PDF/JPEG/PNG); storage key unique |
| `TravelVisaCases` (+ columns) | `ChecklistTemplateId`, `ChecklistVersion` the case was built from | |

**Phase 4 (built)** — migration `TravelNexusUmrahMigration`

| Table | Purpose | Key constraints |
|---|---|---|
| `TravelHotels` | Hotel catalog (Makkah, Madinah, Jeddah, Taif, Other) | unique (tenant, name, city); stars 1–5 |
| `TravelPackages` | Umrah / Hajj package: nights split, visa type, inclusions, currency | code unique per tenant; Draft / Published / Archived |
| `TravelPackageHotels` | Default stays in travel order | nights > 0 |
| `TravelPackagePrices` | Adult / child / infant price per room type, in the package currency | unique (package, room type); DECIMAL(18,2) |
| `TravelGroups` | Group from a package: number (`GRP-yyyy-nnnnn`), dates, capacity, leader, `VisaCaseId` | return ≥ departure; Cancelled needs reason |
| `TravelGroupMembers` | Pilgrim = traveler on a group with booking party, room preference, price category, quoted price + currency | one active row per (group, traveler); Registered / Confirmed / Cancelled |
| `TravelGroupHotels` | The group's stays (copied from the package at creation, editable) | check-out > check-in, within group dates |
| `TravelGroupRooms` | Rooms per stay: type, capacity from type, gender policy Male / Female / Family | |
| `TravelRoomAssignments` | One bed per pilgrim per stay | unique (tenant, stay, member) |
| `TravelGroupTransport` | Legs: airport / intercity / ziyarat; optional `FleetBookingId` (read-only link, same tenant) | |
| `TravelGroupStatusHistory` | Append-only group and pilgrim events, including every manifest generation | event type CHECK |
| `TravelVisaCases` (changed) | `GroupId` added; `CustomerId` now nullable | `CK_TravelVisaCases_Owner`: customer **xor** group; one case per group |
| `TravelSettings` (+ column) | `GroupPrefix` (default `GRP`) | |

**Phase 5 (built)** — migration `TravelNexusBookingsMigration`

| Table | Purpose | Key constraints |
|---|---|---|
| `TravelAirports` (platform) | IATA airports with IANA time zone, seeded from `TravelAirportCatalog` (~100 airports) | PK IATA code; no TenantId (reference data) |
| `TravelSuppliers` | Airlines, hotels, bed banks, consolidators, transport; contact, billing currency | unique (tenant, name); delete refused while services use it |
| `TravelBookings` | Booking file `BKG-yyyy-nnnnn`: owner, optional enquiry / visa case, agent, branch | `CK_TravelBookings_Owner`: customer **xor** group; Open / Completed / Cancelled |
| `TravelFlightBookings` | PNR, airline PNR, direction (Outbound, Return, RoundTrip, OneWay, Internal), ticketing time limit, sell / cost + currency | OnHold needs PNR + time limit; Confirmed / Ticketed need PNR; PNR unique per tenant among non-cancelled |
| `TravelFlightSegments` | Airline, flight no., airports, `DepartLocal` / `ArriveLocal` + derived `DepartUtc` / `ArriveUtc`, cabin, baggage | FK airports; arrive ≥ depart (UTC) |
| `TravelFlightPassengers` | Traveler (and group pilgrim) on a flight, passenger type, ticket number + status | unique (flight, traveler); ticket number unique per tenant; Issued / Voided / Refunded need a number |
| `TravelHotelBookings` | Catalog hotel, supplier, optional group stay link, dates, meal plan, confirmation no., sell / cost + currency | check-out > check-in; confirmed statuses need a confirmation no. |
| `TravelHotelBookingRooms` / `Guests` | Rooms per type; guest travelers | unique per booking |
| `TravelBookingCancellations` | Per service (or one flight passenger): reason, supplier / customer penalty, refund from supplier / to customer, refund status | amounts ≥ 0 and need a currency |
| `TravelBookingHistory` | Append-only file and service events | event type CHECK |
| `TravelSettings` (+ column) | `BookingPrefix` (default `BKG`) | |

**Phase 6A (built)** — migration `TravelNexusFinanceMigration` (the travel ledger; fleet `CustomerInvoices` / `Payments` untouched)

| Table | Purpose | Key constraints |
|---|---|---|
| `TravelInvoices` | Customer invoice in one currency; number allocated on issue (`TINV-yyyy-nnnnn`); optional booking / visa case / group context | Draft / Issued / Void; Total = Subtotal − Discount ≥ 0; issued needs number + dates; void needs reason |
| `TravelInvoiceLines` | Snapshot lines; `SourceType` + `SourceId` for sold items (Flight, Hotel, VisaServiceFee, VisaEmbassyFee, GroupMember) | **unique (tenant, source) where not voided** — an item sits on one live invoice only |
| `TravelInvoiceInstallments` | Schedule; must add up to the total | amount > 0 |
| `TravelPayments` | Receipts (`TRCP-…`): method, reference, amount + currency, status Pending / Completed / Failed / Refunded, reconciliation | **idempotency key unique per tenant** |
| `TravelPaymentAllocations` | Payment → invoice with the recorded exchange rate and the amount applied in invoice currency | unique (payment, invoice) |
| `TravelCreditNotes` | `TCN-…` against an issued invoice, optional Phase 5 cancellation link | Issued / Void; idempotency key |
| `TravelRefunds` | `TRF-…` against an invoice's credit balance **or** a payment's advance, optional cancellation link | invoice xor payment; idempotency key |
| `TravelFinanceHistory` | Append-only invoice / payment / credit / refund events | event type CHECK |
| `TravelSettings` (+ columns) | `ReceiptPrefix`, `CreditNotePrefix`, `RefundPrefix`, `DefaultPaymentTermsDays` | |

**Later phases (planned)**
- Suppliers (6B; profiles exist since Phase 5): `TravelSupplierRates`, `TravelSupplierBills`, `TravelSupplierPayments`,
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

**Documents and checklists (Phase 3).** "Request documents" (Draft → DocumentsPending) snapshots the Active
checklist for the case's visa type (else the country-wide one) into per-applicant requirements, evaluating
applicant conditions (adult/minor at the travel date, gender, travelling with a mahram) in the same transaction.
Applicants added later get requirements from the case's own checklist version; a DocumentsVerified case returns to
DocumentsPending when a newcomer brings mandatory documents. "Mark documents verified" is allowed only when every
mandatory requirement of every active applicant is Verified or Waived; verifying a document with a minimum validity
requires an expiry date that satisfies it. Documents change only while the case is DocumentsPending.

**Passport scan.** `POST api/travel/passport/scan` runs the existing OCR service and an ICAO 9303 TD3 MRZ parser
(check digits, OCR-noise correction, alpha-3 → alpha-2 countries). The image is not stored and OCR text is not
logged; the traveler form is pre-filled for the user to confirm.

**Umrah groups (Phase 4).** `TravelGroupStateMachine` owns transitions and capability checks:

```
Draft → Open → RegistrationClosed → VisaProcessing → Ready → Departed → Returned → Closed
          ↑──── Reopen ────┘                    ↑── Unready ──┘
Draft … Ready → Cancelled (reason)
```

- **Open** needs a published package and at least one stay. **Registration** only while Open, within capacity
  (checked under `UPDLOCK`); a traveler cannot be in two groups with overlapping dates. The quoted price comes from
  the package price for the pilgrim's room type and price category.
- **StartVisaProcessing** needs every active pilgrim Confirmed and a package visa type. In one transaction it opens
  a single visa case owned by the group (`CustomerId` NULL, `GroupId` set) with every confirmed pilgrim as an
  applicant; documents and decisions then use the Phase 2/3 screens. A group case accepts only its active pilgrims.
- **MarkReady** needs the visa case decided, an approved visa for every confirmed pilgrim, and a room for every
  confirmed pilgrim in every stay, and (since Phase 5) an issued outbound and return ticket. **Depart** is refused more than
  a day before the departure date.
- **Cancelling a pilgrim** after the visa case opened removes the applicant (before submission) or records a
  Withdrawn decision (after submission). This is a **second step, not atomic** with the cancellation; if it fails the
  response says so and the case must be updated by hand (the ERP toast asks the user to check the case).
- **Rooming.** `TravelRoomAllocator.Suggest` proposes rooms for one stay: family units (same booking party, joined
  by mahram and child→adult links) in Family rooms of the preferred type; other adults by gender in Male / Female
  rooms; never over capacity. It is a preview — nothing is saved until the user saves. `Validate` runs on every save:
  capacity, gender policy, family rooms only for one booking party or mahram-linked pilgrims, Confirmed pilgrims only,
  one bed per pilgrim per stay.
- **Manifest.** `GET api/travel/groups/{id}/manifest` (`Travel.Group.Manifest`) writes a **fail-closed** audit event
  and a history row before returning data; passport numbers are masked without `Travel.Traveler.ViewPII`. The ERP
  builds the PDF (jspdf + autotable) and Excel (xlsx: Pilgrims, Hotels, Transport sheets) in the browser from that
  response, so masked values stay masked; there is no server PDF library.

**Bookings (Phase 5).** A booking file belongs to a customer or an Umrah group and holds flight and hotel services.

- **File**: Open → Completed (every non-cancelled service finished — flights ticketed, hotels checked out or no-show —
  and the last service in the past) · Completed → Open (Reopen) · Open → Cancelled (reason; only once every service is
  cancelled). Services change only while the file is Open.
- **Flight**: Requested → OnHold (PNR + ticketing time limit; Hold again extends it) → Confirmed → Ticketed; Requested →
  Confirmed directly. Ticket needs ≥ 1 segment, ≥ 1 passenger and every passenger's ticket issued (13 digits). Passengers
  are fixed once ticketed; segments, amounts and notes stay editable (schedule changes). A single passenger is cancelled
  by voiding or refunding the ticket; cancelling the flight sets every issued ticket to Voided or Refunded.
- **Hotel**: Requested → Confirmed (confirmation no.) → CheckedIn (on/after check-in) → CheckedOut; Confirmed → NoShow
  (on/after check-in); cancel from Requested / Confirmed. A hotel booking of a group file may link one of the group's
  stays; confirming it copies the confirmation number to the stay.
- **Times.** Flight times are entered as printed (airport-local). The server derives UTC from the airport's IANA zone
  (`TravelFlightTimes`): a local time skipped by a DST change is refused; an ambiguous one takes the earlier instant;
  unknown airports are refused. Ordering, the list's travel dates and the group gate use these values.
- **Cancellations.** Penalties and refunds are in the service currency; refund to customer + customer penalty ≤ sell
  amount, refund from supplier + supplier penalty ≤ cost amount. Refund status: Pending → ReceivedFromSupplier →
  PaidToCustomer → Closed (steps may be skipped forward, never back); NotApplicable when nothing is refunded. This is
  tracking only — Phase 6 turns it into credit notes and refunds.
- **Group gate.** Mark ready now also requires every confirmed pilgrim to hold an issued ticket on a ticketed round
  trip, or on both an outbound and a return flight, in a non-cancelled file owned by the group. Cancelling a pilgrim
  never changes bookings; the response says when the pilgrim still holds an issued ticket.
- **Documents.** The ERP builds the itinerary (flights with local times, hotels, group transfers) and the hotel voucher
  with jspdf from the booking detail. Neither carries passport numbers; passengers' passports on the detail are masked
  without `Travel.Traveler.ViewPII`.
- **KPIs.** `/bookings/summary` returns files per status (zero-filled, total = sum, "all time") and flights on hold whose
  ticketing deadline falls within the next 72 hours (labelled as such). An inverted travel-date range returns an empty list.

**Customer finance (Phase 6A).** Revenue is recognised from issued invoices, never from payment status.

- **Invoices** are drafted from billable sources — a customer booking file's priced flights and hotels, a visa case's quoted
  service and embassy fees, a customer's pilgrims in a group at their quoted package price — plus manual lines. Group-owned
  booking files are not billable per service (the pilgrims' package price covers them). Every line's currency must be the
  invoice currency. Drafts are editable; issuing allocates the gap-free number and fixes the dates (due = last installment,
  or issue date + payment terms). Issued invoices are immutable — corrections are credit notes. Void only with no payments
  (even pending), credit notes or refunds; voiding releases the source items.
- **Balance** (always computed): `Total − Credited − (Paid − Refunded)`, where Paid counts Completed (cleared) payments only;
  Pending payments reserve their amount. Payment status shown: Unpaid / PartiallyPaid / Paid / CreditDue (negative balance).
  Installments — or, without a schedule, the whole invoice on its due date — are covered oldest-due first; overdue = what
  fell due before today minus what is covered.
- **Payments** may be in another currency: the cashier records the rate (invoice units per payment unit); the applied
  amount is rounded to 2 dp half away from zero. Allocation is capped by the invoice's open balance (less pending
  reservations) and by what is left on the payment; both are re-checked under `UPDLOCK` in the write transaction.
  Unallocated money stays on account (advance). Pending → Completed / Failed; Completed → Failed while not reconciled or
  refunded; Failed allocations stop counting. Reconciliation marks cleared non-cash payments with a bank reference.
- **Credit notes** are capped by the invoice's uncredited total; **refunds** by an invoice's credit balance or a payment's
  unallocated advance. A refund linked to a Phase 5 cancellation moves that cancellation to PaidToCustomer in the same
  transaction. Receipts, credit notes and refunds carry the client's idempotency key — a retried submit returns the original.
- **Reports**: per-currency summary (outstanding and credit due all time, overdue as of today, collected this month,
  unallocated advances; invoice counts zero-filled, total = sum), customer balances, statement of account per currency
  with opening and running balance (date range applied; inverted range → no entries), reconciliation lists (unreconciled
  payments, sold-but-not-invoiced, changed-since-invoiced), group pilgrim payments per booking party. The ERP builds invoice,
  receipt, credit note and statement PDFs client-side.
- **Not in 6A**: taxes, supplier bills / payments, commissions, profitability (6B), gateway collection, reminders (Phase 7).

## 5. Security model

- **Tenant isolation.** Every statement filters on `TenantId`; joins repeat the predicate. Inserts that depend
  on a parent use `INSERT … SELECT … WHERE parent.TenantId = @TenantId`. Travel tables are listed in the SG-01
  guard (`Sg01TenantSqlGuardScanner.TenantOwnedTables`); `TravelNexusTenantGuardTests` fails the build if any
  TravelNexus statement misses the predicate.
- **Branch scope.** `ITravelBranchScopeResolver` turns the user's data scope into a branch clamp. Records outside
  the clamp are reported as not found, never as forbidden, so their existence is not revealed.
- **Permissions.** Phase 1: `Travel.View`, `Travel.Settings`, `Travel.Customer.View`, `Travel.Customer.Manage`,
  `Travel.Traveler.ViewPII`. Phase 2: `Travel.Enquiry.View/Manage`, `Travel.Visa.View/Manage`, `Travel.Visa.Decide`.
  Phase 3: `Travel.Document.Manage` (upload, request), `Travel.Document.Verify` (verify, reject, waive),
  `Travel.Document.Download` (open files). Checklists are managed with `Travel.Settings`.
  Phase 4: `Travel.Package.View/Manage` (packages and the hotel catalog), `Travel.Group.View/Manage` (groups,
  pilgrims, stays, rooms, transport), `Travel.Group.Manifest` (manifest — passport data, audited).
  Phase 5: `Travel.Supplier.View/Manage`, `Travel.Booking.View`, `Travel.Booking.Manage` (files, flights, passengers,
  tickets, hotels, check-in/out), `Travel.Booking.Cancel` (cancellations, penalties, refund status).
  Phase 6A: `Travel.Finance.View`, `Travel.Invoice.Manage` (draft, issue), `Travel.Payment.Manage` (record, allocate,
  clear / fail), `Travel.Finance.Adjust` (void, credit notes, refunds — financial authorisation kept separate),
  `Travel.Finance.Reconcile`.
  Each phase's migration grants its own codes to SUPER_ADMIN, TENANT_ADMIN and OWNER of the tenants that exist when it
  runs. Every permission maps to module `TRAVEL_AGENCY`, so it is inert for companies without the module.
  **Known gap:** companies provisioned later get no Travel permissions until an admin grants them in role management
  (the same applies to RENTAL); a grant-on-module-enable hook is planned for Phase 8.
- **Private documents.** Passport and visa files never use `IFileStorageService` or `/uploads`.
  `ITravelDocumentStorage` writes to a dedicated private Azure container (`TravelDocuments:AzureContainerName`,
  default `travel-documents`, no SAS ever issued) or, locally, to `private-storage/travel-documents` outside the
  publicly served folder (git-ignored). Keys are `tenant-{id}/case-{id}/{guid}.{ext}` and validated before use.
  Uploads are checked by magic bytes (must match the extension), capped at 10 MB and hashed (SHA-256). Files are only
  streamed by `GET api/travel/documents/files/{id}` (`Travel.Document.Download`, `no-store`, `nosniff`), which writes
  a **fail-closed** audit event before opening the file. The ERP fetches files as blobs and revokes object URLs on
  close. nginx allows 12 MB bodies only on `/api/travel/(documents|passport)/`.
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
| GET | `api/travel/checklists` · `{id}` | Travel.View |
| POST / PUT / DELETE | `api/travel/checklists`, `{id}`, `{id}/items`, `{id}/activate`, `{id}/new-version` | Travel.Settings |
| GET | `api/travel/documents/visa-cases/{caseId}` · `missing` | Travel.Visa.View |
| POST / DELETE | `api/travel/documents/requirements/{id}/files` (multipart), `visa-cases/{caseId}/requests`, `requirements/{id}` | Travel.Document.Manage |
| POST | `api/travel/documents/requirements/{id}/verify` · `reject` · `waive` | Travel.Document.Verify |
| GET | `api/travel/documents/files/{fileId}` (stream, audited) | Travel.Document.Download |
| POST | `api/travel/passport/scan` (multipart, image) | Travel.Customer.Manage |
| GET | `api/travel/hotels` (city, isActive) · `api/travel/packages` (status, packageType) · `packages/{id}` | Travel.Package.View |
| POST / PUT / DELETE | `api/travel/hotels[/{id}]`, `packages[/{id}]`, POST `packages/{id}/publish` · `archive` | Travel.Package.Manage |
| GET | `api/travel/groups` (search, status, packageId, departureFrom/To) · `summary` · `by-customer/{id}` · `{id}` · `{id}/stays/{stayId}/rooming/suggest` | Travel.Group.View |
| POST / PUT / DELETE | `api/travel/groups[/{id}]`, `{id}/transition`, `{id}/members`, `members/{mid}/confirm` · `cancel`, PUT `members/{mid}`, `{id}/stays[/{sid}]`, PUT `stays/{sid}/rooming`, `{id}/transport[/{lid}]` | Travel.Group.Manage |
| GET | `api/travel/groups/{id}/manifest` (audited, `no-store`) | Travel.Group.Manifest |
| GET | `api/travel/airports` (search) | Travel.View |
| GET / POST / PUT / DELETE | `api/travel/suppliers[/{id}]` (supplierType, isActive) | Supplier.View / Supplier.Manage |
| GET | `api/travel/bookings` (search, status, ownerType, customerId, groupId, travelFrom/To, ticketingDue) · `summary` · `by-customer/{id}` · `by-group/{id}` · `{id}` | Travel.Booking.View |
| POST / PUT / DELETE | `api/travel/bookings[/{id}]`, `{id}/transition`, `{id}/flights[/{fid}]`, `flights/{fid}/transition`, PUT `flights/{fid}/tickets`, `{id}/hotels[/{hid}]`, `hotels/{hid}/transition` | Travel.Booking.Manage |
| POST / PUT | `api/travel/bookings/{id}/flights/{fid}/cancel`, `flights/{fid}/passengers/{pid}/cancel`, `hotels/{hid}/cancel`, PUT `cancellations/{cid}/refund-status` | Travel.Booking.Cancel |
| GET | `api/travel/finance/invoices` (search, status, paymentStatus, customerId, currencyCode, issueFrom/To, dueFrom/To, overdue) · `invoices/{id}` · `billable` · `customers/{id}/open-invoices` · `payments` (receivedFrom/To, reconciled…) · `payments/{id}` · `credit-notes` · `refunds` · `summary` · `balances` · `customers/{id}/statement` · `reconciliation/uninvoiced` · `reconciliation/changed-lines` · `groups/{id}` | Travel.Finance.View |
| POST / PUT / DELETE | `api/travel/finance/invoices[/{id}]`, `invoices/{id}/issue` | Travel.Invoice.Manage |
| POST / DELETE | `api/travel/finance/payments`, `payments/{id}/allocations[/{invoiceId}]`, `payments/{id}/status` | Travel.Payment.Manage |
| POST | `api/travel/finance/invoices/{id}/void`, `credit-notes`, `credit-notes/{id}/void`, `refunds` | Travel.Finance.Adjust |
| POST | `api/travel/finance/payments/{id}/reconcile` | Travel.Finance.Reconcile |

## 7. Roadmap

| Phase | Scope | Status |
|---|---|---|
| 0 | Repository analysis and this document | Done |
| 1 | Module, permissions, navigation, configuration, customers, travelers | Done |
| 2 | Enquiries, follow-ups, conversion; visa cases, applicants, status pipeline + history | Done |
| 3 | Checklist templates and versions, private document upload, verification, missing-documents list, passport OCR | Done |
| 4 | Packages, Umrah groups, pilgrims, hotels, rooms, transport / Ziyarat, manifest | Done |
| 5 | Flight and hotel bookings, PNR / tickets, vouchers, itineraries, cancellations | Done |
| 6A | Customer invoices, receipts, installments, credit notes, refunds, balances, statements, reconciliation | Done |
| 6B | Supplier rates, bills and payments, supplier ledgers, commissions (earned and payable), profitability | Planned |
| 7 | Reminder rules and dispatch, dashboard, reports | Planned |
| 8 | Live-SQL end-to-end tests, security review, deployment notes, user guide | Planned |

Each phase gets its own plan, migration, tests and commits on the feature branch.

## 8. Finding outside TravelNexus (for the audit owner)

Existing Nexus uploads (driver CNIC images, vehicle documents, trip files) are publicly readable:

- `Program.cs` serves `{ContentRoot}/uploads` with `UseStaticFiles` **before** authentication, and nginx proxies
  `/uploads/` — anyone with a file URL can download it, with no login.
- `AzureBlobStorageService` returns read SAS links valid for `AzureSasExpiryDays` (default **365 days**), or a plain
  public URL when `AzurePublicBaseUrl` is set.

This is not part of SG-01..52. TravelNexus does not use that path. Fixing it (an authenticated, audited stream
like TravelNexus's, short SAS lifetimes, removing the static `/uploads` mapping) needs its own SG issue because the
ERP shows these URLs directly in `<img>` and `<a href>` in many places.
