# Phase 3 — MCP Tool Catalog (STOP for review)

**Status:** Complete (approved). Phase 4 data retrieval documented in `AI-MCP-Phase4-DataRetrieval.md`.  
**Date:** 2026-10-26  
**Repos:** SheikhGo-MCP `0.13.0`+ catalog; Phase 4 bumps MCP to `0.14.0`.

---

## 1. MCP inventory and ownership map

| Domain | Owner process | Ownership path | Tenant AI? |
|--------|---------------|----------------|------------|
| Architecture / Code | `sheikhgo-mcp` (single stdio server) | `SheikhGo-MCP/src/architecture`, `src/search`, `src/git`, `src/writes` | **Denied** |
| Database | same | `src/database` | Yes (live SELECT + approved counts) |
| Bookings | same | `src/bookings` | Yes |
| Fleet | same | `src/fleet` | Yes |
| GPS / Traccar | same | `src/gps` | Yes (DB lat/lng; optional Traccar env) |
| SmartOps | same | `src/smartops` | Yes |
| **Analytics** | same | `src/analytics` (**new**) | Yes (KPIs) |
| **Logs** | same | `src/logs` (**new**) | Elevated (`Ai.ViewProviderHealth` / `Ai.Manage`) |
| Security / Audit | same | `src/security` | `list_audit_events` + domain catalog elevated; file scanners **denied** for tenant AI |
| Bridge / orchestrator | API process | `Backend/.../Services/Ai/Mcp/*` | Filters tools by auth |

**No duplicate MCP servers.** ERP console groups are labels over one `sheikhgo-mcp` process.

---

## 2. Tool catalog and schemas summary

### Database (AI allowlist)
`db_connection_status`, `list_tables`, `describe_table`, `run_readonly_query`, **`get_approved_entity_counts`**

### Bookings / Fleet / GPS / SmartOps
Unchanged executable set (list/get/summarize/status). Typed Zod schemas + tenantId; timeouts/row caps via Database MCP.

### Analytics (≠ Logs; expanded in Phase 5)
`get_analytics_domain`, `get_revenue_summary`, `get_trip_statistics`, `get_vehicle_utilization`, `get_fuel_cost_summary`, `get_driver_performance`, plus Phase 5: `get_fleet_inventory_summary`, `get_gps_coverage_summary`, `get_maintenance_kpi_summary`, `get_booking_analytics`, `get_revenue_breakdown`, `get_period_comparison` — see `docs/AI-MCP-Phase5-Analytics.md`

### Logs (new — ≠ Analytics; elevated)
`get_logs_domain`, `get_ai_assistant_errors`, `get_ai_usage_summary`, `get_integration_diagnostics`

### Security / Audit
- Tenant AI: `get_security_domain`, **`list_audit_events`** (elevated)
- Cursor/dev only (denied on AI bridge): `scan_secrets`, `audit_permissions`, `audit_api_auth`

**Error contract:** MCP tools return `isError` + text message; bridge truncates content and never invents rows. Elevated denial returns explicit permission error (not advertised in schemas for unauthorized users).

---

## 3. Updated registrations / integration

| Change | Location |
|--------|----------|
| New MCP tools registered | `SheikhGo-MCP/src/mcp/tools.ts` |
| Allowlist + permission gates | `McpAllowlist.cs` |
| Bridge filters discovery + execute | `SheikhGoMcpBridge.cs` (+ `ICurrentUserService`, scoped DI) |
| Orchestrator prompt prefers narrow tools | `AiMcpToolCallingLoop.cs` |
| ERP catalog export | `scripts/export-tool-catalog.mjs` → `mcp-catalog.json` / fallback |
| Docs | `docs/AI-MCP-Bridge.md`, this file, MCP playbook |

`GET /api/ai/mcp/tools` returns **only tools permitted for the current user**.

---

## 4. Tests / builds (executed 2026-10-26)

| Command | Result |
|---------|--------|
| `cd SheikhGo-MCP && npm run typecheck` | Pass |
| `cd SheikhGo-MCP && npm test` | **94 passed**, 0 failed |
| `cd SheikhGo-MCP && npm run export:catalog` | 83 tools → ERP assets |
| `cd SheikhGo-MCP && npm run build` | Pass |
| `dotnet test … --filter McpAllowlist\|McpDatabase\|AiMcpToolCalling` | **79 passed**, 0 failed |

---

## 5. Deferred tools and dependencies

| Item | Why deferred |
|------|----------------|
| `search_logs` / arbitrary app log files | No centralized log store; would invent incomplete tooling |
| `get_slow_requests` | Needs APM / request metrics pipeline |
| Finance MCP (`get_invoices`, …) | Keep revenue under Analytics; full finance domain later |
| CRM MCP (`get_customers` list tool) | Counts via `get_approved_entity_counts`; full CRM later |
| Testing / DevOps MCP groups | Still **planned** in catalog — not advertised as running |
| Maps geocoding / ETA | Explicitly out of scope — no Maps keys |
| Promoting file security scanners to tenant AI | Exposes repo internals; keep Cursor-only |
| Deduplicating keyword `IAiTool` analytics vs MCP Analytics | Both work; unify UX in a later phase if desired |

---

## 6. STOP

Phase 3 complete for Ali review. **Do not start Phase 4** until approved.
