# sheikhgo-erp — Angular ERP rules

Angular · RxJS · TypeScript strict · SCSS · Bootstrap
The admin/ERP front end. This is the app the audit in docs/audit/audit.md covers.

## Build and test
- `npm start` · `ng build --configuration production` · `ng test` · `ng lint`

## API base URL
- NEVER hardcode the API host. Read it at runtime from assets/config.json via
  APP_INITIALIZER, not baked into the bundle. The current raw-IP host is SG-09.

## Tenant context
- ONE tenant context for the whole app, in TenantContextService, shown in the global
  header. Modules do not invent their own company pickers.
- Every list request sends the active tenant. Platform roles may pick "All companies";
  components must handle an all-tenants response (rows carry a company column).
- Do not add a page-local company selector. If a page needs one, the global switcher
  is broken — fix that instead.

## KPI cards — the rule that fixes most of our bugs
- A KPI is never computed in a component from a partially loaded list.
- Aggregates come from a dedicated summary endpoint. One call, one source of truth.
- Every card states its window in the label: "last 7 days", "all time", "this month".
  Never place a weekly figure beside a lifetime figure without saying so.
- If a set of counters should sum to a total, add a unit test asserting it.

## Money and formatting
- Currency from tenant settings, never a hardcoded symbol. No `$` and no `PKR`
  literals in templates — currency pipe with the tenant's ISO code.
- Dates through a shared pipe using the tenant's timezone and format.

## Freshness
- Telemetry (speed, ignition, position, battery) renders with a freshness badge:
  Live (<5 min), Stale (5-30 min), Offline (>30 min). Never render a cached value
  styled as live. One shared badge component, not per-page markup.

## PII
- CNIC renders masked (last 4) unless the user holds Customer.ViewPII. A reveal calls
  the audited unmask endpoint — never un-hide a value already in the DOM.
  CNIC is excluded from default CSV exports.

## RxJS hygiene
- Shared service observables use shareReplay({bufferSize:1, refCount:true}).
- Never subscribe in ngOnInit AND use an async pipe on the same stream — that is how
  we got duplicate API calls on every dashboard load.
- Unsubscribe via takeUntilDestroyed.
- Do not fetch pageSize=50 to render 5 rows.

## Menus
- Menu visibility uses a View permission, never a Create permission.
