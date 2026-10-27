# Phase 4 — Secure SQL Server Data Retrieval (STOP for review)

**Status:** Complete (approved path to Phase 5). Analytics expansion: `docs/AI-MCP-Phase5-Analytics.md`.  
**Date:** 2026-10-26  
**Repos:** SheikhGo-MCP `0.14.0` (+ `0.15.0` Analytics Phase 5) + Sheikh-Travel-System AI bridge allowlist / router / orchestrator prompt.

---

## 1. Goal

Factual AI questions return **actual authorized SQL Server results**. Failures and empty sets never invent numbers. Prefer narrow Phase 4 tools over model-authored SQL.

---

## 2. Schema assumptions (inspected — not invented)

| Table | TenantId | Soft-delete | Used by |
|-------|----------|-------------|---------|
| `Vehicles` | yes | `IsDeleted` | count, details, maintenance join |
| `Drivers` | yes | yes | details |
| `Trips` | yes | yes | trip summary (`TripDate`) |
| `Bookings` | yes | yes | booking summary (`PickupTime`) |
| `FuelLogs` | yes | yes | fuel expenses |
| `Payments` | yes | yes | revenue (`Status = 3` Paid) |
| `Maintenance` | yes | yes | maintenance due (join via `Vehicles`) |
| `AssignmentHistory` | yes | yes | active driver↔vehicle enrichment |
| `VehicleCurrentLocation` | no TenantId column | n/a | lat/lng only — **no Maps keys** |

Domain enums mirrored in MCP status maps (`VehicleStatus`, `DriverStatus`, `TripStatus`, `BookingStatus`, `PaymentStatus`, `MaintenanceStatus`, `FuelType`).

### Gaps / deferred (documented — do not invent)

| Concept | Why |
|---------|-----|
| Invoices / AR | No Invoice table on AI path; revenue = paid `Payments` |
| Predictive schedule dues | SmartOps `get_maintenance_reminders` ≠ Fleet `get_maintenance_due` (work-orders) |
| Full CRM list tool | Counts via `get_approved_entity_counts` only |
| Maps geocoding / ETA | Explicitly out of scope |

---

## 3. Phase 4 tools (implemented or hardened)

| Tool | Implementation | Notes |
|------|----------------|-------|
| `get_vehicle_count` | New — parameterized COUNT + byStatus | Default excludes Draft(5)/Retired(4) |
| `get_vehicle_details` | Wraps `get_vehicle` | `found=false` empty success |
| `get_driver_details` | Wraps `get_driver` | same empty contract |
| `get_trip_summary` | New period histogram | `TripDate` window |
| `get_fuel_expenses` | Wraps Analytics fuel SQL + optional vehicle | liters + tenant currency |
| `get_maintenance_due` | Existing Fleet SQL + Phase 4 envelope | empty list ≠ failure |
| `get_booking_summary` | New period rollup | `PickupTime` window |
| `get_revenue_summary` | Existing Analytics + empty flag | Paid only |
| `get_database_schema` | **Approved catalog** (mode=`approved` default) | `mode=full` adds file scan for Cursor; file-only `analyze_database` remains AI-denied |
| `plan_approved_query` | Controlled planner | Maps NL → approved tool; rejects unsafe |

Also: `run_readonly_query` gains `approvedOnly` to reject unapproved tables (last resort).

---

## 4. Data rules enforced

- Parameterized SQL via `mssql` inputs / Dapper-equivalent MCP path  
- TenantId required at data layer for business tools; bridge overwrites client tenantId  
- Date range validation (max 366 days; from &lt; to)  
- Row caps + query timeouts from `SHEIKHGO_DB_*` config  
- SQL guard blocks DML/DDL/EXEC/multi-statement  
- Connection string never returned (`db_connection_status` / redaction)  
- Zero rows → `ok: true`, `empty: true` / `found: false`  
- Connection / timeout errors throw → MCP `isError` → assistant must not invent  

---

## 5. Broad questions

`plan_approved_query` + `validateApprovedSqlPlan` — schema metadata + approved joins only. No invented column meanings.

---

## 6. Files changed

### SheikhGo-MCP
- `src/database/approved-schema.ts` (new)
- `src/database/query-planner.ts` (new)
- `src/database/business-retrieval.ts` (new)
- `src/mcp/tools.ts`
- `scripts/export-tool-catalog.mjs`
- `package.json` → `0.14.0`
- `tests/phase4-business-retrieval.test.ts` (new)

### Sheikh-Travel-System
- `Backend/.../Mcp/McpAllowlist.cs`
- `Backend/.../Mcp/McpDatabaseRecordRouter.cs`
- `Backend/.../Mcp/SheikhGoMcpBridge.cs`
- `Backend/.../Ai/AiMcpToolCallingLoop.cs`
- `Backend/.../Tests/AI/McpAllowlistTests.cs`
- `Backend/.../Tests/AI/McpDatabaseRecordRouterTests.cs`
- `docs/AI-MCP-Phase4-DataRetrieval.md` (this file)

---

## 7. Tests / commands

```bash
cd /Users/alirazatahir/Projects/SheikhGo-MCP && npm run typecheck && npm test
cd /Users/alirazatahir/Projects/SheikhGo-MCP && npm run export:catalog && npm run build

dotnet test /Users/alirazatahir/Projects/Sheikh-Travel-System/Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj \
  --filter "FullyQualifiedName~McpAllowlist|FullyQualifiedName~McpDatabaseRecordRouter|FullyQualifiedName~AiMcpToolCalling"
```

| Command | Result |
|---------|--------|
| `npm run typecheck` | Pass |
| `npm test` | **116 passed**, 0 failed |
| `npm run export:catalog` | 90 tools → ERP assets |
| `npm run build` | Pass |
| `dotnet test … --filter McpAllowlist\|McpDatabaseRecordRouter\|AiMcpToolCalling` | **92 passed**, 0 failed |

---

## 8. STOP

Phase 4 complete for Ali review. **Do not start Phase 5** until approved.
