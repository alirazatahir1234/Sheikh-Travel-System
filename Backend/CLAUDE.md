# Backend — .NET API rules

.NET 8 · ASP.NET Core · CQRS + MediatR · Dapper (primary) + EF Core · SQL Server · SignalR · Serilog

## Build and test
- Build: `dotnet build` from this folder
- Test:  `dotnet test`
- Never run `dotnet ef database update` or any migration against a connection string
  you did not just read from user-secrets for Development. Ask me first, always.

## Tenant isolation — non-negotiable
- Every query that reads tenant-owned data MUST filter on TenantId.
- TenantId comes from ITenantContext (resolved from the JWT claim), never from a
  request body, query string, or route parameter. A client-supplied tenant id is an
  authorization bug, not a feature.
- Dapper gives us no global query filter, so the filter is manual and explicit: every
  SQL string touching a tenant-owned table carries `WHERE TenantId = @TenantId`
  (or a join whose root is filtered).
- Tenant-owned tables: Vehicles, Drivers, DriverDocuments, ComplianceDocuments, Trips,
  Bookings, Customers, Invoices, Payments, Settlements, FuelLogs, MaintenanceOrders,
  Inspections, Assignments, Branches, Departments, Users, GpsDevices, Alerts,
  Notifications, RentalBookings, RentalRates, AllowanceRules.
- Platform-scope tables (no TenantId): Tenants, Plans, Modules, MenuCatalog, Migrations.
- A SUPER_ADMIN may read across tenants ONLY through an endpoint explicitly marked
  platform-scope that writes an audit event naming the tenants read.
- New repository method => a test asserting it returns nothing for another tenant's id.

## CQRS / MediatR
- One command or query per file under Features/<Area>/<Name>/.
- Handlers are thin: validate, call repository, map, return. No SQL in handlers.
- Validation via FluentValidation behaviour, not inline ifs.
- Cross-cutting concerns (logging, validation, tenant guard, audit) are pipeline
  behaviours. Tempted to add the same line to many handlers? Write a behaviour.

## Money
- decimal(18,2). Never double or float.
- Every monetary column carries a CurrencyCode (ISO 4217). Never assume PKR.
- Revenue is recognised from Invoices, never from Payment.Status.
- Payment.Status is Pending | Completed | Failed | Refunded. "PartiallyPaid" belongs to
  an Invoice balance, not to a payment. Do not reintroduce it on Payment.

## Enums over the wire
- Serialize as strings, never ints. Every enum returned to a client has a matching
  TypeScript union in sheikhgo-erp. Change one, say so in your summary.
- Returning an int the UI must map is how we ended up with "Unknown" everywhere.

## Dates
- Store UTC. Convert at the edge only.
- Any endpoint accepting fromDate/toDate MUST apply them. An empty range returns an
  empty set — never fall back to unfiltered data.

## Audit
- Every write to a tenant-owned entity, every permission change, every cross-tenant read,
  and every login success/failure writes an AuditEvent via IAuditWriter.
- AuditEvent: TenantId, ActorUserId, Category, Severity, Outcome, Entity, EntityId, UtcAt.

## Security
- Authorization is by permission, never by role name. There is no
  `if (role == "SUPER_ADMIN")` anywhere — if you find one, flag it, do not copy it.
- Never log PII. CNIC, full phone numbers and exact coordinates are PII.
- Never put a secret in appsettings.json. User-secrets locally, Key Vault in Azure.

## Style
- Follow the patterns already in the file you are editing.
- Async all the way; no .Result or .Wait().
- Serilog structured logging with named properties, not string interpolation.
