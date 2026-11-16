# SheikhGo — Fix Playbook for Claude Code & Cursor

Nov 16, 2026 · @Ali Raza Tahir · Companion to the Super Admin Platform Audit — 22 P0/P1 issues, three repos

## Which tool for which repo

Use both, but not for the same kind of work. The split that matters: **Claude Code runs long, multi-file, reasoned changes with a plan you approve first. Cursor is faster for the tight edit loop inside one file you are already reading.** Most of the 22 fixes are the first kind.

| Work | Tool | Why |
| --- | --- | --- |
| SG-01 tenant isolation sweep across every repository and handler | Claude Code (VS Code), plan mode | Needs to read 100+ files, build a list, then change them consistently. Cursor's context window fills before it finishes reading |
| SG-02/03/14 invoice + payment chain | Claude Code, plan mode | New entities, migrations, handlers, DTOs, endpoints — a design decision, not an edit |
| SG-05/06/07/08 RBAC, tenant switcher, audit, MFA | Claude Code | Cross-cutting across API and Angular |
| SG-16 menu permission, SG-26 `$` symbol, SG-30 KPI sum | Cursor (Cmd+K inline) | One-line fixes you can see on screen |
| Angular component KPI rewrites (SG-12, SG-13, SG-15) | Cursor Composer | You are staring at the template; the change is local and visual |
| Flutter driver app changes (SG-04 staleness states) | Claude Code | Separate repo, small, needs the API contract in context |
| Writing the regression tests | Claude Code with a subagent | Boring, parallelisable, needs no supervision |

### The three-repo problem, and the one setting that solves it

Your repos are separate: `sheikhgo-api` (.NET 8), `sheikhgo-web` (Angular), `sheikhgo-driver` (Flutter). Several fixes span two of them — SG-06 (tenant switcher) is an API endpoint plus an Angular header component; SG-04 (device staleness) is an API rule plus Angular badges plus Flutter.

Don't open three windows and copy-paste between them. Instead create a parent folder and open **that** in VS Code:

```
~/dev/sheikhgo/
  CLAUDE.md            ← shared rules + cross-repo map
  sheikhgo-api/        ← own git repo, own CLAUDE.md
  sheikhgo-web/        ← own git repo, own CLAUDE.md
  sheikhgo-driver/     ← own git repo, own CLAUDE.md
  docs/audit/          ← the audit export lives here
```

One Claude Code session at the parent sees all three. Git still works per-repo — Claude Code will run `git` in whichever subfolder it is touching, and you commit and branch in each repo independently. The nested `CLAUDE.md` files are picked up automatically when Claude reads a file under that folder, so API rules apply to API files and Angular rules to Angular files without you re-stating them.

Cursor works the same way: open the parent folder as the workspace, keep per-repo rules in each `.cursor/rules/`.

### Give both tools the audit

Export the audit doc to Markdown and save it as `docs/audit/2026-11-16-super-admin-audit.md` in the parent folder. Then every prompt in this playbook can say *"read docs/audit/2026-11-16-super-admin-audit.md, section SG-01"* instead of you re-explaining the bug. That one file is the highest-leverage context you can give either tool.

## One-time setup

### Claude Code in VS Code (macOS)

```bash
# install
npm install -g @anthropic-ai/claude-code

# set up the parent workspace
mkdir -p ~/dev/sheikhgo/docs/audit
cd ~/dev/sheikhgo
# move or clone your three repos in here

# first run — authenticates in the browser
claude
```

Then in VS Code install the **Claude Code** extension from the marketplace and open `~/dev/sheikhgo` as the folder. The extension gives you the sidebar panel, diff view in the editor, and `Cmd+Esc` to jump between terminal and panel. Everything below works identically from the terminal if you prefer it.

Two settings to change on day one:

- **Turn on plan mode by default for this project.** Press `Shift+Tab` twice to cycle into plan mode, or start a session with `claude --permission-mode plan`. In plan mode Claude reads and reasons but writes nothing until you approve a plan. For a codebase you are about to change in 22 places, this is the difference between a reviewable PR and a mess.
- **Review the permission prompts rather than allowing everything.** Allow `Read`, `Grep`, `Glob`, `dotnet build`, `dotnet test`, `ng build`, `ng test`. Do **not** blanket-allow `Bash`. Never allow anything that touches your production connection string or runs `dotnet ef database update` against production.

### Cursor

Install Cursor, open the same `~/dev/sheikhgo` folder. Settings → Models: enable Claude for Composer and for Cmd+K. Settings → Rules: point it at `.cursor/rules/` (section below).

In Cursor, use **Cmd+K** for an inline edit in the file you are looking at, and **Composer (Cmd+I)** in *Agent* mode for a change across a handful of files you can name. When you find yourself typing "and also search the rest of the codebase for…", stop and move that task to Claude Code.

### Connect Claude Code to your SQL Server and Jira

Two MCP servers are worth the ten minutes, because several fixes need to see real data:

```bash
# SQL Server — read-only user, never the app's write user
claude mcp add mssql -- npx -y @modelcontextprotocol/server-mssql

# Jira, if you want tickets created from the audit
claude mcp add atlassian --transport sse https://mcp.atlassian.com/v1/sse
```

For the SQL connection, create a dedicated login with `db_datareader` on a **restored copy** of production, not production itself. Several of the P0 investigations (how many compliance rows have a null TenantId, which payments can be matched to bookings by amount and date) are much faster if Claude can run the query itself — but it must never be able to write.

You already have a `/mcp` console in the ERP with 117 tools. Do not connect Claude Code to that one until SG-22 is fixed and it is environment-gated; right now it is a production surface.

### What never goes near either tool

Connection strings, JWT signing keys, the Traccar admin token, Meta app secrets, Azure credentials. Keep them in user-secrets or Key Vault and add this to `.gitignore` and to both tools' ignore files:

```
appsettings.Production.json
**/secrets.json
*.pfx
.env*
```

If a secret has ever been committed, rotate it rather than deleting the file — git history keeps it.

## CLAUDE.md for the API repo

This is the single highest-value thing you will write today. Every rule here stops a class of bug from coming back, and it is read on every session automatically. Save as `sheikhgo-api/CLAUDE.md`.

```markdown
# SheikhGo API — working rules

.NET 8 · ASP.NET Core · CQRS + MediatR · Dapper (primary) + EF Core · SQL Server · SignalR · Serilog
Multi-tenant SaaS. Tenants are fleet operators. Cross-tenant data leakage is a P0 incident.

## Build and test
- Build: `dotnet build SheikhGo.sln -c Debug`
- Test:  `dotnet test SheikhGo.sln`
- Run:   `dotnet run --project src/SheikhGo.Api`
- Never run `dotnet ef database update` or any migration against a connection string
  you did not just read from user-secrets for Development. Ask me first, always.

## Tenant isolation — non-negotiable
- Every query that reads tenant-owned data MUST filter on TenantId.
- TenantId comes from ITenantContext (resolved from the JWT claim), never from a
  request body, query string, or route parameter. A client-supplied tenant id is an
  authorization bug, not a feature.
- Dapper gives us no global query filter, so the filter is manual and must be explicit:
  every SQL string that touches a tenant-owned table carries `WHERE TenantId = @TenantId`
  (or a join whose root is filtered).
- Tenant-owned tables: Vehicles, Drivers, DriverDocuments, ComplianceDocuments, Trips,
  Bookings, Customers, Invoices, Payments, Settlements, FuelLogs, MaintenanceOrders,
  Inspections, Assignments, Branches, Departments, Users, GpsDevices, Alerts, Notifications,
  RentalBookings, RentalRates, AllowanceRules.
- Platform-scope tables (no TenantId): Tenants, Plans, Modules, MenuCatalog, Migrations.
- A SUPER_ADMIN may read across tenants ONLY through an endpoint that is explicitly
  marked platform-scope and writes an audit event naming the tenants read.
- When you add a repository method, add a unit test that asserts it returns nothing for
  a different tenant's id. No test, no merge.

## CQRS / MediatR conventions
- One command or query per file under Features/<Area>/<Name>/.
- Handlers are thin: validate, call repository, map, return. No SQL in handlers.
- Validation via FluentValidation behaviour, not inline ifs.
- Commands return the created/updated id or a Result, never an entity.
- Cross-cutting concerns (logging, validation, tenant guard, audit) are pipeline
  behaviours. If you are tempted to add the same line to many handlers, write a behaviour.

## Money
- Money is decimal(18,2). Never double or float.
- Every monetary column carries a CurrencyCode (ISO 4217). Never assume PKR.
- Revenue is recognised from Invoices, never from Payment.Status.
- Payment.Status is Pending | Completed | Failed | Refunded. "PartiallyPaid" is a
  property of an Invoice balance, not of a payment. Do not reintroduce it on Payment.

## Enums over the wire
- Serialize enums as strings, not ints, and keep the API contract stable.
- Every enum returned to the UI must have a matching TypeScript union in sheikhgo-web.
  If you change one, say so in your summary so the web repo gets updated.
- Never return an int the UI has to map — that is how we ended up with "Unknown" everywhere.

## Dates and time
- Store UTC. Convert at the edge only.
- Any endpoint that accepts fromDate/toDate MUST apply them. If the range yields no rows,
  return an empty set — never fall back to unfiltered data.

## Audit
- Every write to a tenant-owned entity, every permission change, every cross-tenant read,
  and every login success/failure writes an AuditEvent via IAuditWriter.
- AuditEvent carries: TenantId, ActorUserId, Category, Severity, Outcome, Entity, EntityId, UtcAt.

## Security
- Authorization is by permission, never by role name. There is no `if (role == "SUPER_ADMIN")`
  anywhere — if you find one, flag it, do not copy it.
- Never log PII. CNIC, full phone numbers and exact coordinates are PII.
- Never put a secret in appsettings.json. User-secrets locally, Key Vault in Azure.

## Style
- Follow the patterns already in the file you are editing. Do not reformat untouched code.
- Async all the way; no .Result or .Wait().
- Serilog structured logging with named properties, not string interpolation.

## Working with me
- Default to plan mode for anything touching more than three files. Show me the plan first.
- One issue per branch: `fix/SG-01-compliance-tenant-isolation`.
- When you finish, give me: files changed, the migration (if any), the test you added,
  and the exact steps to verify it by hand.
- If the audit document docs/audit/ describes the issue, read it before starting.
```

The three rules doing the most work here are the tenant-owned table list (so Claude never has to guess whether a table needs filtering), the ban on role-name checks (SG-05 cannot be reintroduced), and the "never fall back to unfiltered data" rule (SG-15).

## CLAUDE.md for the Angular and Flutter repos

### sheikhgo-web/CLAUDE.md

```markdown
# SheikhGo Web — working rules

Angular (standalone components, signals where sensible) · RxJS · TypeScript strict · SCSS · Bootstrap
ERP front end for a multi-tenant fleet platform. API base URL comes from runtime config.

## Build and test
- `npm start` · `ng build --configuration production` · `ng test`
- Lint must pass: `ng lint`

## API base URL
- NEVER hardcode the API host. It is read at runtime from assets/config.json, not baked
  into the bundle at build time. The current raw-IP host is a known defect (SG-09).

## Tenant context
- There is ONE tenant context for the whole app, held in TenantContextService and shown
  in the global header. Modules do not invent their own company pickers.
- Every list request sends the active tenant. Platform roles may select "All companies";
  components must handle an all-tenants response (rows carry a company column).
- Do not add a page-local company selector. If a page needs one, the global switcher is broken.

## KPI cards — the rule that fixes most of our bugs
- A KPI is never computed in a component from a partially loaded list.
- Aggregates come from a dedicated summary endpoint, one call, one source of truth.
- Every card states its window in the label: "last 7 days", "all time", "this month".
  Never place a weekly figure next to a lifetime figure without saying so.
- If a set of counters should sum to a total, add a unit test asserting it.

## Money and formatting
- Currency comes from tenant settings, never a hardcoded symbol. No `$` literals, no `PKR`
  literals in templates — use the currency pipe with the tenant's ISO code.
- Dates through a shared pipe using the tenant's timezone and date format.

## Freshness
- Telemetry values (speed, ignition, position, battery) render with a freshness badge:
  Live (<5 min), Stale (5–30 min), Offline (>30 min). Never render a cached value
  styled as live. The badge is a shared component, not per-page markup.

## PII
- CNIC renders masked (last 4) unless the user holds Customer.ViewPII, and a reveal
  calls the audit endpoint. CNIC is excluded from default CSV exports.

## RxJS hygiene
- Shared service observables use shareReplay({bufferSize:1, refCount:true}).
- Never subscribe in ngOnInit AND use an async pipe on the same stream — that is how we
  got duplicate API calls on every dashboard load.
- Unsubscribe via takeUntilDestroyed.
- Do not fetch pageSize=50 to render 5 rows. Ask for what you display.

## Menus and permissions
- Menu visibility uses a View permission, never a Create permission.

## Working with me
- Plan mode for anything touching more than three files.
- Branch per issue: fix/SG-xx-short-name.
- Tell me if a change needs a matching API change in sheikhgo-api.
```

### sheikhgo-driver/CLAUDE.md

Short, because the driver app has only a few of these fixes.

```markdown
# SheikhGo Driver — working rules

Flutter · Dart · Riverpod · GoRouter · Dio · Hive · SignalR · Workmanager

- API base URL from remote config / build flavor, NEVER a hardcoded IP. See SG-09.
- Offline-first: mobile networks drop. Queue writes in Hive, reconcile on reconnect,
  and show the user what is pending rather than failing silently.
- Telemetry shown to the driver carries the same Live / Stale / Offline states as the ERP.
  A cached speed is never shown as current.
- Never log or cache CNIC or exact customer coordinates to disk.
- Run `flutter analyze` and `flutter test` before you tell me you are done.
- Plan mode for anything touching more than three files.
```

### The parent CLAUDE.md

At `~/dev/sheikhgo/CLAUDE.md`, keep it to a map and the cross-repo rules:

```markdown
# SheikhGo workspace

Three independent git repos:
- sheikhgo-api/    .NET 8 API      (own CLAUDE.md)
- sheikhgo-web/    Angular ERP     (own CLAUDE.md)
- sheikhgo-driver/ Flutter app     (own CLAUDE.md)

The audit driving current work: docs/audit/2026-11-16-super-admin-audit.md
Issue ids SG-01 .. SG-49 refer to that document. Read the relevant section before starting.

Cross-repo rules:
- An API contract change (enum, DTO, endpoint) is not done until the web and driver
  repos are updated or an issue is opened in each. Say which repos are affected.
- Commit in each repo separately. Never stage across repos.
- Branch names match across repos for the same issue: fix/SG-04-telemetry-staleness.
```

## Cursor rules

Cursor reads `.cursor/rules/*.mdc` files. The difference from `CLAUDE.md`: Cursor rules carry frontmatter that decides *when* a rule loads, so you can scope a rule to a glob instead of paying for it on every request. Keep them short — Cursor injects them into a smaller context than Claude Code works with.

Don't duplicate the whole `CLAUDE.md` here. Put the handful of rules that prevent the bugs you are most likely to reintroduce while editing inline.

### sheikhgo-api/.cursor/rules/tenant-isolation.mdc

```
---
description: Tenant isolation for all data access
globs: ["**/Repositories/**/*.cs", "**/Features/**/*.cs", "**/*Repository.cs", "**/*Handler.cs"]
alwaysApply: false
---

Every SQL statement touching a tenant-owned table carries WHERE TenantId = @TenantId.
TenantId comes from ITenantContext only — never from request body, query string or route.
Tenant-owned: Vehicles, Drivers, ComplianceDocuments, Trips, Bookings, Customers,
Invoices, Payments, Settlements, FuelLogs, MaintenanceOrders, Inspections, Assignments,
Branches, Departments, Users, GpsDevices, Alerts, Notifications, RentalBookings, AllowanceRules.
When you add a repository method, add a test asserting it returns nothing for another tenant.
```

### sheikhgo-api/.cursor/rules/money-and-enums.mdc

```
---
description: Money, enums and date handling
globs: ["**/*.cs"]
alwaysApply: false
---

Money is decimal(18,2) with an ISO 4217 CurrencyCode. Never double, never an assumed PKR.
Revenue is recognised from Invoices, never from Payment.Status.
Enums serialize as strings over the wire, never ints.
fromDate/toDate parameters must be applied; an empty range returns an empty set,
never an unfiltered fallback.
No authorization by role name. Permission checks only.
```

### sheikhgo-web/.cursor/rules/ui-rules.mdc

```
---
description: Angular KPI, currency and telemetry rules
globs: ["**/*.ts", "**/*.html"]
alwaysApply: false
---

No hardcoded currency symbols. Currency pipe with the tenant's ISO code.
No hardcoded API host. Runtime config only.
KPI values come from a summary endpoint, not computed from a partial list.
Every KPI label states its time window.
Telemetry renders with a Live / Stale / Offline freshness badge; a cached value is never
styled as live.
CNIC renders masked unless the user holds Customer.ViewPII.
Shared streams use shareReplay({bufferSize:1, refCount:true}); never subscribe in ngOnInit
and use an async pipe on the same stream.
Menu visibility uses a View permission, never Create.
```

### One rule worth marking alwaysApply

```
---
description: Scope discipline
alwaysApply: true
---

Change only what I asked for. Do not reformat, rename or "improve" untouched code.
If you believe a wider change is needed, say so and wait — do not make it.
If a change needs a matching change in another repo, name the repo and the file.
```

That last one matters more in Cursor than in Claude Code. Cursor's Composer is enthusiastic about adjacent refactors, and a 400-line diff where you expected 6 lines is how a one-line permission fix turns into a lost afternoon of review.

## The working loop

The same five steps for every issue. It takes about two minutes of discipline per fix and it is what keeps a 22-issue sprint reviewable.

1. **Branch first.** `git checkout -b fix/SG-01-compliance-tenant-isolation`. One issue, one branch, one PR, in each repo it touches.
2. **Plan mode.** `Shift+Tab` `Shift+Tab` until the prompt shows plan mode, then paste the prompt from the pack below. Claude reads and comes back with a plan. Read the plan properly — this is the review that costs you two minutes and saves you two hours.
3. **Approve, then let it run.** Accept the plan and let it work. Resist the urge to interrupt mid-change; interrupt only if the plan was wrong.
4. **Review the diff in VS Code, not in the chat.** The extension shows real diffs in the editor. Read every changed file. Run the build and tests yourself.
5. **Commit with the issue id.** `git commit -m "fix(SG-01): scope compliance queries to tenant"`. When a fix spans repos, use the same branch name and the same id in both.

### When to use a subagent

Claude Code can spawn subagents that do work in their own context and report back a summary. Three places in this project where that is worth it:

- **The SG-01 audit sweep.** "Use an Explore subagent to list every file containing raw SQL that reads a tenant-owned table, and report the ones with no TenantId predicate." A sweep like that would otherwise eat your main session's context before any fixing starts.
- **Writing the regression tests.** Once a fix is agreed, a subagent can write the tenant-isolation tests for twenty repository methods while you review the main change.
- **A second-opinion review.** After a P0 fix, `"spawn a subagent to review the diff on this branch as a hostile reviewer looking for missed tenant filters"`. You wrote the fix with Claude; have a fresh context check it.

### Context hygiene

- `/clear` between issues. Carrying SG-02's context into SG-04 makes answers worse, not better.
- `/compact` if a single issue runs long and you want to keep going.
- Point at files explicitly — `@src/SheikhGo.Infrastructure/Repositories/ComplianceRepository.cs` — rather than describing them. Cheaper and more accurate.
- Keep the audit file reference in the prompt rather than pasting the audit text each time.

### What to never let the agent do

| Never | Why |
| --- | --- |
| Run a migration against production, or any DB you did not point it at deliberately | SG-29 already auto-applies migrations at startup; do not add a second uncontrolled path |
| `git push --force`, or push to `main` | One issue, one branch, one reviewed PR |
| Touch `appsettings.Production.json` or anything with a secret | Rotate rather than clean up afterwards |
| Connect to the production `/mcp` console | It is an unreviewed surface until SG-22 is fixed |
| "Fix all the other issues it noticed while it was in there" | Scope creep is how a 6-line fix becomes an unreviewable diff. Note them, do them as their own issue |
| Delete or rewrite the audit file | It is your record of what was wrong and when |

### A note on reviewing agent work in this codebase specifically

For the tenant-isolation fixes, do not accept "I added the filter" at face value. The failure mode is subtle: a query gets `WHERE TenantId = @TenantId` on the outer select while an inner join or a secondary query in the same handler stays unfiltered. Ask for the full SQL of every changed query in the summary and read it. That one habit is the difference between fixing SG-01 and thinking you fixed SG-01.

## Prompt pack — the 8 P0 issues

Each block is a complete prompt. Paste it into Claude Code **in plan mode**, in the repo's branch. They assume `CLAUDE.md` and the audit file are in place, so they are short on background and long on acceptance criteria — that is deliberate; the acceptance criteria are what you review the plan against.

### SG-01 — Compliance tenant isolation (do this one first)

Run this in two passes. Pass 1 finds the damage, pass 2 fixes it. Do not let it do both in one go.

```
Pass 1 — investigation only, change nothing.

Read docs/audit/2026-11-16-super-admin-audit.md section SG-01.

In sheikhgo-api, use an Explore subagent to find every data-access path that reads a
tenant-owned entity, and classify each one:
  A) filters on TenantId from ITenantContext      -> OK
  B) filters on a TenantId that came from the request -> AUTHORIZATION BUG
  C) no TenantId filter at all                     -> LEAK

Cover: Dapper SQL strings, EF Core queries, stored procedure calls, and any raw
ExecuteReader. Include read models and report queries, not just repositories.

Output a markdown table: file, method, table(s) read, classification, the WHERE clause
as it stands. Start with anything touching ComplianceDocuments or DriverDocuments,
because that is the confirmed leak. Write the table to docs/audit/SG-01-sweep.md.

Do not change any code in this pass.
```

Read that table yourself before pass 2. Then:

```
Pass 2 — fix.

Using docs/audit/SG-01-sweep.md, fix every row classified LEAK or AUTHORIZATION BUG.

Rules:
- TenantId comes from ITenantContext only.
- Add the filter to the inner query too, not just the outer select. Joins count.
- Where a query is legitimately platform-scope (Tenants, Plans, Modules, MenuCatalog),
  leave it, and mark it with a // PLATFORM-SCOPE comment explaining why.
- Do not change behaviour beyond adding the filter. No refactoring.

Then add a pipeline behaviour or repository base guard that makes this harder to
regress, and tell me the trade-offs of the approach you chose before implementing it.

For each fixed method, add an xUnit test: seed two tenants, query as tenant A,
assert zero rows from tenant B.

In your summary give me the full SQL of every query you changed, before and after.
```

**Acceptance:** compliance page under Hannan Khokhar Travels shows documents for Munna Driver only; `docs/audit/SG-01-sweep.md` has no remaining LEAK rows; every changed method has an isolation test.

### SG-02 + SG-03 + SG-14 — The money chain (one design, one session)

These three are one problem. Do not fix them separately.

```
Read docs/audit/2026-11-16-super-admin-audit.md sections SG-02, SG-03 and SG-14.

Context: we have 5 completed bookings worth PKR 43,142 and 9 payments worth PKR 39,824,
and zero invoices. Payments have no booking link. Revenue is being read off
Payment.Status == Paid, so PKR 35,624 of partial payments is invisible.

Design first, in plan mode, and show me the design before writing code:

1. Invoice aggregate: Invoice + InvoiceLine, numbered per tenant with a configurable
   series (prefix, year, sequence), Status = Draft | Issued | PartiallyPaid | Paid |
   Overdue | Cancelled, with Subtotal, TaxAmount, Total, AmountPaid, Balance,
   CurrencyCode. Money is decimal(18,2).
2. Invoice is generated when a Trip reaches Completed, via a domain event handler —
   not inline in the trip handler.
3. Payment gets a required InvoiceId. Payment.Status becomes
   Pending | Completed | Failed | Refunded. "PartiallyPaid" moves off Payment and is
   derived on Invoice from Balance.
4. A refund is a negative-signed payment or a separate CreditNote — recommend one and
   say why.
5. Revenue, Collected and Outstanding AR are read from Invoices, never from payments.

Then tell me, as a separate section of the plan, how you would backfill:
  - an invoice per existing completed booking
  - a best-effort match of the 9 existing payments to those invoices by amount and date,
    flagging anything ambiguous for manual review rather than guessing.

Migrations: write them, do not run them. I will run them against a restored copy first.
```

**Acceptance:** completing a trip produces an issued invoice; a payment cannot be saved without an invoice; dashboard Collected equals the sum of invoice `AmountPaid`; the backfill script reports matched vs ambiguous rather than silently pairing.

### SG-04 — Telemetry staleness (API + web + Flutter)

```
Read docs/audit/2026-11-16-super-admin-audit.md section SG-04.

Today a GPS device whose last fix is 5 hours old is reported as Online, with ignition
ON and speed 15 km/h, all served from cache, and the vehicle shows as ON ROUTE.

In sheikhgo-api:
- Introduce a single DeviceFreshness rule used by every endpoint that reports device or
  vehicle state: Live (lastSeen < 5 min), Stale (5–30 min), Offline (> 30 min).
  Thresholds come from configuration, not constants scattered in handlers.
- Every telemetry DTO returns lastSeenUtc, freshness, and a boolean isCached alongside
  the value. Never a bare speed or ignition value.
- A vehicle may only be reported ON ROUTE if its device is Live or it has an active
  assignment with a manual override; decide which and tell me.
- Fix the online/offline counters so Dashboard, Live Map and Devices all read from the
  same summary endpoint.

Find every place that currently computes online/offline/moving/idle/parked and list them
before you change anything — the audit found four screens disagreeing.
```

Then the web side, in `sheikhgo-web`:

```
Read docs/audit/2026-11-16-super-admin-audit.md section SG-04 and SG-12.

Build one shared FreshnessBadge component (Live / Stale / Offline + relative lastSeen)
and use it everywhere a telemetry value renders: devices table, live map popups,
vehicle list, vehicle detail, dashboard tiles.

A Stale or Offline value renders dimmed with the badge and the age ("5h ago").
Never render a cached speed or ignition state as if it were live.

All the fleet-status counters on Dashboard, Live Map and Devices must read from the one
summary endpoint, not be computed per component.
```

**Acceptance:** the device last seen 5h ago shows Offline on all four screens; no screen reports it as moving; the three screens report identical counts.

### SG-05 — Give SUPER\_ADMIN a real permission set

```
Read docs/audit/2026-11-16-super-admin-audit.md section SG-05.

The SUPER_ADMIN role row has 0 permissions and 0 users, yet the signed-in super admin
has 119 effective permissions. Access is coming from a hard-coded check on the role
name somewhere in the authorization pipeline.

Step 1: find every place authorization is decided by role name rather than permission.
Search for comparisons against "SUPER_ADMIN", "TENANT_ADMIN", any RoleName equality,
[Authorize(Roles=...)] attributes, and any IsSuperAdmin helper. List them all with file
and line before changing anything.

Step 2: propose, with trade-offs, how to model platform scope properly:
  - a Platform.* permission family (Platform.Tenant.Read, Platform.Tenant.Write,
    Platform.Audit.ReadAll, Platform.User.ReadAll, Platform.Impersonate, ...)
  - SUPER_ADMIN seeded with all of them via a migration
  - a new PLATFORM_AUDITOR role that is read-only across all tenants
  - the distinction between tenant scope and platform scope being explicit in the
    authorization handler, not implied by a role name

Step 3: once I approve, implement it and remove the bypass. Any endpoint that reads
across tenants requires an explicit Platform.* permission AND writes an audit event
naming the tenants it read.

Add tests: a SUPER_ADMIN with the permission revoked must be denied.
```

**Acceptance:** Access Control shows SUPER\_ADMIN with a non-zero permission count; no role-name comparison remains in the authorization path; revoking a platform permission actually denies access.

### SG-06 — Global tenant switcher (API + web)

This unblocks three screens, so it pays for itself fastest. API side:

```
Read docs/audit/2026-11-16-super-admin-audit.md sections SG-06 and SG-11.

Add platform-scope tenant context:
- GET /api/platform/tenants/switchable — returns every tenant the caller may act in,
  INCLUDING Suspended and Expired ones, each with a status flag. Requires
  Platform.Tenant.Read. Today suspended tenants are silently missing, which makes
  suspension a one-way door.
- The active tenant for a platform user is carried per-request in an X-Tenant-Id header
  validated against that switchable list — never trusted blindly. A tenant user's
  tenant still comes from their JWT claim and the header is ignored for them.
- Support an "all tenants" mode for list endpoints that declare themselves platform-scope.
  Those responses include a companyName on every row, and every such request writes
  an audit event.

List which existing endpoints you would mark platform-scope and why, before implementing.
```

Web side:

```
Read docs/audit/2026-11-16-super-admin-audit.md section SG-06.

We have four different company-scoping mechanisms today: an inline banner on
vehicles/drivers, a Select Tenant dropdown on three platform pages, a scope chip bar in
the GPS module, and nothing at all on nine other pages. They are not synchronised.

Replace all four with one TenantContextService plus a tenant switcher in the global
header, next to the user menu:
- shows the active company name at all times, so you always know which company you are in
- lists switchable tenants including suspended ones, visually marked
- offers "All companies" for users holding Platform.Tenant.Read
- persists the selection, and emits a change every module subscribes to
- sends X-Tenant-Id on every request via an HTTP interceptor

Then remove the page-local selectors from vehicles, drivers, the GPS scope bar and the
three platform pages, and unblock Security Center, Audit Center and Workspace
Management, which currently say "Select a company from the platform tenant switcher"
and cannot be used at all.

List every component you will touch before you start.
```

**Acceptance:** one switcher in the header; Security Center, Audit Center and Workspace Management all load; switching company refreshes every open module; suspended tenants are selectable.

### SG-07 — Make the audit trail readable

```
Read docs/audit/2026-11-16-super-admin-audit.md section SG-07.

Audit level is set to Always and audit.login_events is true, yet Audit Center returns
zero rows. First establish which is true:
  (a) events are being written and the read path is broken, or
  (b) events are not being written at all.
Query the audit table (read-only connection) and tell me the row count, the date range
and the top categories before changing anything.

Then:
- Make the audit read endpoint work for platform roles without requiring a tenant,
  with an "all companies" mode (depends on SG-06).
- If events are missing, add an IAuditWriter pipeline behaviour so every command writes
  one automatically rather than relying on each handler remembering.
- Audit events must be append-only: no update or delete path, enforced in the database.
- Also fix SG-36: /audit-logs and /platform/audit-center render the same component at
  two routes. Keep one, redirect the other.

Tell me what is NOT being audited today that should be: permission changes, role
assignment, cross-tenant reads, subscription changes, PII reveals, login failures.
```

**Acceptance:** Audit Center returns rows without a tenant selected; a permission change and a login failure both appear in it within seconds; the table has no update or delete path.

### SG-08 — MFA and real password policy

```
Read docs/audit/2026-11-16-super-admin-audit.md section SG-08.

Security Center displays MFA required, Trusted devices, Password history and IP
restriction, and the UI text admits none of them enforce anything. Last updated is "—"
on every policy, so nothing has ever been configured.

Part 1 — be honest in the UI today (sheikhgo-web, small change, ship immediately):
  mark every non-enforcing policy with a "Not enforced yet" badge and disable its toggle.
  A security console that overstates what it does is worse than one that admits a gap.

Part 2 — implement TOTP MFA in sheikhgo-api:
  - enrolment (QR + secret), verification, and recovery codes
  - MFA required at login when compliance.mfa_required is true for the tenant
  - ALWAYS required for any user holding a Platform.* permission, regardless of the
    tenant flag — the super admin is the account worth protecting most
  - store secrets encrypted; recovery codes hashed; rate-limit verification attempts
  - every enrolment, challenge, success and failure writes an audit event

Part 3 — turn on password.complexity and make it actually enforce on registration,
change-password and admin reset.

Do not implement device trust or IP allowlisting yet. Say so in the UI instead.
```

**Acceptance:** a platform-role login cannot complete without a TOTP code; Security Center shows a real Last updated timestamp after a policy change; non-enforcing toggles are visibly marked.

## Prompt pack — the P1 issues, grouped

Related issues go in one session, because fixing them separately means reading the same code three times. SG-14 is not here — it is part of the money chain above.

### Group A — SG-09, the API host (infrastructure, not code)

Mostly DNS and Azure work, but the client change matters:

```
Read docs/audit/2026-11-16-super-admin-audit.md section SG-09.

The API is served from https://20-174-1-230.sslip.io — a raw IP on a free third-party
wildcard DNS service. The IP is baked into the web bundle and presumably the Flutter app.

In sheikhgo-web: move the API base URL out of environment.ts and into a runtime config
file (assets/config.json) fetched before app bootstrap via APP_INITIALIZER, so the host
can change without a rebuild. Find every hardcoded occurrence of the host first and list
them.

In sheikhgo-driver: same idea — base URL from build flavour plus remote config, never a
literal. List every occurrence.

Do not change CORS or anything server-side yet; tell me what will need to change in the
API's CORS policy and cookie domain when we move to api.sheikhgo.com.
```

The infrastructure half — register `api.sheikhgo.com`, point it at the Azure IP, cert via your CDN, put Front Door or Cloudflare in front, then update CORS — is a half-day of portal work, not an agent task.

### Group B — SG-12, SG-13, SG-15: KPIs and time windows

The single most valuable Angular session. Run it after SG-04's summary endpoint exists.

```
Read docs/audit/2026-11-16-super-admin-audit.md sections SG-12, SG-13, SG-15 and the
Dashboard section.

Three related defects:
- Utilization shows 0% on the dashboard and 100% on /vehicles and /fleet/assignments.
- Net profit shows PKR -24,330 by subtracting a maintenance cost the Maintenance module
  reports as PKR 0, and it mixes a weekly revenue figure with a lifetime fuel figure.
- Cards labelled "last 30 days" and "this week" render data from May to October. The API
  is called WITH fromDate/toDate and the result ignores them.

Fix the pattern, not the screens:
1. Every KPI comes from one summary endpoint per page. No component computes an
   aggregate from a partially loaded list.
2. Every KPI carries its window in the response (windowFrom, windowTo, or "allTime")
   and the label renders it. A card may not sit next to another card with a different
   window unless both labels say so.
3. Net profit is recomputed from the ledger once invoices exist; until then show it as
   unavailable rather than wrong. A wrong number is worse than a missing one.
4. Find where the date range is being dropped — frontend not sending it, backend not
   applying it, or a fallback to unfiltered on an empty result. Check all three.

Add a unit test per counter group asserting the parts sum to the total (this also fixes
SG-30, trips 6 != 1+0+4+0, and SG-31, notification channels summing to 1,931 not 3,101).
```

### Group C — SG-17, SG-20, SG-21: state machines and compliance

```
Read docs/audit/2026-11-16-super-admin-audit.md sections SG-17, SG-20 and SG-21.

Three symptoms of one cause — entity states change independently with nothing
coordinating them:
- Trip TR-2026-2003 is Cancelled while its booking BK-2026-3011 is Completed and billed.
- A driver with status Suspended is attached to a vehicle currently ON ROUTE, and is
  counted as both Active and Suspended.
- All 39 compliance documents have status UNKNOWN and no expiry, KPIs read 0/0/0, yet
  the dashboard raises a document-expiry alert for 1 vehicle.

In sheikhgo-api:
1. Define explicit state machines for Booking and Trip, with the legal transitions and
   the rule that cancelling a trip cancels or flags its booking. Show me the transition
   table before implementing.
2. Add a guard: a driver who is Suspended, has an expired licence, or is on leave cannot
   be assigned or dispatched. Return a domain error, not a silent success.
3. Compliance: make ExpiryDate required on document upload, derive status from it
   (Valid / ExpiringSoon within 30 days / Expired), and have the dashboard alert read
   from the same source as the compliance KPIs. Write a one-off script that lists the
   39 documents with no expiry for manual completion — do not invent dates.
4. Fix the driver KPI so Active and Suspended are mutually exclusive.

Also fix SG-23 while you are in this area: all five allowance rules render Type
"Unknown" because the enum serialises as an int. Serialise enums as strings across the
API and check the Angular union types match.
```

### Group D — SG-18, CNIC and PII

```
Read docs/audit/2026-11-16-super-admin-audit.md section SG-18.

Full 13-digit CNIC numbers render in plain text in the /customers list and in the CSV
export, with no masking and no separate permission.

API:
- Add a Customer.ViewPII permission. List endpoints return CNIC masked to the last 4
  unless the caller holds it. Unmasked retrieval is a separate endpoint that writes an
  audit event naming the customer and the actor.
- Exclude CNIC from the default CSV export. A PII export requires the permission and is
  itself audited.
- Make sure CNIC never reaches Serilog. Add a destructuring policy if needed.

Web:
- Masked display with a reveal action for permitted users; the reveal calls the audited
  endpoint rather than un-hiding a value already in the DOM.

Check whether CNIC is also exposed on driver records and in any report, and tell me.
```

### Group E — SG-19 and SG-10: integrations that fail silently

```
Read docs/audit/2026-11-16-super-admin-audit.md sections SG-19 and SG-10.

SG-19: the only inbound WhatsApp webhook payload we have ever received was rejected with
"Invalid signature". Check, in this order:
  1. the app secret configured in SheikhGo vs the Meta app
  2. whether the HMAC-SHA256 for X-Hub-Signature-256 is computed over the RAW request
     body or over re-serialised JSON — in ASP.NET Core this needs EnableBuffering() and
     reading the body as a stream before model binding. This is the most likely cause.
  3. whether a proxy or middleware mutates the body first
Add a test that replays a known-good Meta payload with a correct signature and asserts
acceptance, plus one with a bad signature asserting rejection.
A rejected payload must raise an alert, not sit in a log with Attempts = 0.

SG-10: 1,748 of 3,101 notifications are marked Failed and the UI exposes no reason.
  - Add a FailureReason and a provider response code to the notification record.
  - Add retry with exponential backoff and a dead-letter state.
  - Expose failure reason in the Notification Center UI.
  - Investigate the 1,748: group by channel and date and tell me what the dominant cause
    is before proposing a fix.
  - Stop the stale reminder loop: a maintenance reminder for 2026-08-08 is still being
    sent on 2026-11-16 because nothing closes it when the task is never actioned.
```

### Group F — SG-11, SG-42, SG-22: platform visibility and the MCP surface

SG-11 and SG-42 fall out of SG-06 almost for free; do them in the same session. SG-22 is a config change you should make on day one:

```
Read docs/audit/2026-11-16-super-admin-audit.md sections SG-11, SG-42 and SG-22.

1. SG-11: the tenant selector omits Suspended companies, so a suspended tenant cannot be
   inspected, re-planned or reactivated. Include them, flagged by status.
2. SG-42: /users shows 5 users for one tenant while the platform has 9. Add a
   cross-tenant user directory for holders of Platform.User.ReadAll, with a company
   column, search across tenants, and the same PII rules as SG-18.
3. SG-22: the /mcp console (14 servers, 117 registered tools, Connect Client for external
   IDEs) is reachable inside the production ERP. Put it behind an environment check so it
   is unavailable in Production, and behind a dedicated Platform.Mcp.Access permission
   elsewhere. Every tool invocation writes an audit event. Tell me if anything in the
   product depends on it at runtime before you gate it.
```

### Quick wins for Cursor, not Claude Code

These are one-line or one-file fixes. Open the file, `Cmd+K`, state the change:

| Issue | File to open | The change |
| --- | --- | --- |
| SG-16 | Menu catalog seed / migration | `Vehicle.Create` → `Vehicle.View` on the Vehicles menu item |
| SG-26 | Maintenance cost widget | Replace the hardcoded `$` with the currency pipe and the tenant's ISO code |
| SG-30 | Trips KPI component | Add a Cancelled counter so the parts sum to the total |
| SG-37 | Menu catalog | Delete the dead `/VehicleList` entry; point Fleet Tracking at `/gps-tracking/live` |
| SG-39 | WhatsApp module breadcrumb | Match the sidebar — it is a top-level group, not under Administration |
| SG-43 | Companies page KPI | Define "active" and label the card accordingly |

## Verification — so these cannot come back

A fix without a test is a fix that returns in three months. Build these six once; after that every prompt in the pack can simply end with "and add the matching test".

| # | What to build | Stops |
| --- | --- | --- |
| 1 | `TenantIsolationTestBase` — seeds two tenants with a full object graph, exposes an assert that runs a query as tenant A and fails if any tenant B row comes back. One test per repository method | SG-01 |
| 2 | A test that scans the source for SQL literals naming a tenant-owned table and fails when there is no TenantId predicate, with an explicit `// PLATFORM-SCOPE` opt-out | SG-01 recurring |
| 3 | A role matrix golden-file test: for each seeded role, the exact set of menu items and endpoints it can reach | SG-05, SG-16 |
| 4 | Counter-sum tests: for every KPI group, parts must equal the total | SG-12, SG-30, SG-31 |
| 5 | Date-window tests: request a range that excludes all seeded data and assert an empty result, never a fallback | SG-15 |
| 6 | Webhook signature tests: a known-good Meta payload accepted, a tampered one rejected | SG-19 |

Number 2 is the one that actually fixes SG-01. The code change fixes today's leak; that test stops the next one.

### Put them in CI

```
Add a GitHub Actions workflow for sheikhgo-api that runs on every PR:
  dotnet build, dotnet test, and fail the build if the tenant-isolation scan test fails.
Add one for sheikhgo-web: ng lint, ng test, ng build --configuration production.
Do not add a deploy step yet — and specifically do not let CI run migrations.
SG-29 already auto-applies migrations at app startup; we are removing that, not adding
a second uncontrolled path.
```

### Manual smoke pass after each P0

Keep this as a checklist and run it after every P0 merge. It takes five minutes and catches what tests miss.

- [ ] Sign in as super admin — header shows a tenant switcher with the active company name
- [ ] Switch company — vehicles, drivers, compliance and maintenance all change together
- [ ] Open /fleet/compliance — only the selected company's drivers appear
- [ ] Open Audit Center — rows load without picking a tenant, and your own login appears
- [ ] Open /gps-tracking/devices — a device last seen hours ago reads Offline, not Online
- [ ] Complete a test trip in a non-production tenant — an invoice is created and numbered
- [ ] Record a payment — it cannot be saved without an invoice, and the invoice balance moves
- [ ] Open /customers — CNIC is masked; reveal requires the permission and appears in the audit
- [ ] Sign in as a Dispatcher — the Vehicles menu is visible and the platform pages are not

### A habit worth keeping

After each P0 fix, ask for a hostile review from a fresh context: *"spawn a subagent to review the diff on this branch as a reviewer trying to find the tenant filter we missed"*. You and Claude wrote the fix together, which means you share the same blind spots. A clean context does not.

## Execution order

The dependencies matter. SG-06 unblocks four other fixes, so it goes early even though it is not the scariest issue. The money chain is a single design and should not be split across sprints.

| Order | Issue | Repos | Depends on | Rough size |
| --- | --- | --- | --- | --- |
| 1 | SG-22 gate the MCP console | api | — | 1 hour |
| 2 | SG-18 mask CNIC | api + web | — | half day |
| 3 | SG-16 menu permission, plus the Cursor quick wins | api + web | — | 2 hours |
| 4 | SG-01 pass 1, the sweep | api | — | half day |
| 5 | SG-01 pass 2, the fix + isolation tests | api | sweep | 3–4 days |
| 6 | SG-06 global tenant switcher | api + web | SG-05 ideally | 3–4 days |
| 7 | SG-05 platform permissions | api | — | 2–3 days |
| 8 | SG-07 audit trail | api + web | SG-06 | 2–3 days |
| 9 | SG-11 + SG-42 platform visibility | api + web | SG-06 | 1–2 days |
| 10 | SG-02 + SG-03 + SG-14 money chain | api + web | — | 1–2 weeks |
| 11 | SG-08 MFA | api + web | SG-05 | 1 week |
| 12 | SG-04 telemetry staleness | api + web + driver | — | 3–4 days |
| 13 | SG-12/13/15 KPIs and windows | web | SG-04, SG-02 | 3–4 days |
| 14 | SG-17/20/21/23 state machines, compliance, enums | api + web | — | 1 week |
| 15 | SG-19 + SG-10 webhook and notifications | api | — | 3–4 days |
| 16 | SG-09 API domain migration | infra + web + driver | — | half day + DNS wait |

### What can run in parallel

If you are working alone, run them in the order above. If you have a second pair of hands, these three tracks barely touch each other:

- **Track 1 (you, Claude Code):** SG-01 → SG-05 → SG-06 → SG-07 → SG-08. All platform and security. This is the track that decides whether you can sell to an enterprise buyer.
- **Track 2:** the money chain, SG-02/03/14, then SG-13. Mostly new code, few conflicts with track 1.
- **Track 3:** SG-04 telemetry, then SG-12/15. Touches GPS and dashboard only.

SG-09 is DNS and portal work that can happen any time; start the domain registration early because propagation and certificate issuance are wall-clock time, not work.

### Branch and PR convention

```bash
# in each affected repo, same branch name
git checkout -b fix/SG-06-global-tenant-switcher

# commits carry the id
git commit -m "feat(SG-06): tenant switcher in global header"

# PR title carries it too
# fix(SG-06): global tenant switcher — api + web
```

When a fix spans repos, open both PRs and link them in each description. Merge the API one first.

### Where to stop and think, not code

Three of these are product decisions that Claude should not make for you, and that you should settle before the relevant prompt runs:

1. **Does an expired subscription block login?** Sheikh Travel is Expired and its 5 users still work normally. Deciding this is a business call — hard block, grace period, or read-only mode — and it changes the SG-02 and subscription work.
2. **Tax on invoices.** The Security Center carries a `compliance.vat_enabled` flag set to false. If SheikhGo will ever sell into the UAE or Saudi Arabia, VAT has to be in the invoice model from day one, not bolted on. Decide before SG-02 is designed.
3. **Is there an "all companies" data view, or only an "all companies" admin view?** Letting a platform role read every tenant's operational data is convenient and is also the thing an enterprise buyer will ask you about. Define it narrowly and audit it, or do not build it.

### Keep the audit file alive

As each issue closes, mark it in `docs/audit/2026-11-16-super-admin-audit.md` with the PR link and date. In three months that file is your evidence trail for a security questionnaire, and it is the context that makes every future Claude Code session on this codebase sharper.
