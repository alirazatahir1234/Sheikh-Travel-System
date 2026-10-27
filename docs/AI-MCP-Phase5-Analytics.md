# Phase 5 — Analytics MCP (STOP for review)

**Status:** Complete — reviewed / superseded for next work by Phase 6 Logs. **No further Analytics scope here.**  
**Date:** 2026-10-26  
**Repos:** SheikhGo-MCP `0.15.0` (+ later `0.16.0` for Logs) + Sheikh-Travel-System AI bridge allowlist / router / orchestrator prompt.

---

## 1. Goal

Expose **reproducible business KPIs** to the AI Assistant and Angular via structured Analytics MCP tools. Prefer SQL aggregation over loading rows into the model. Never invent metrics (especially **fleet health %**). Align booking periods with Phase 4 (`PickupTime`). Fix fuel **grand totals** so they are never summed from truncated TOP N lists.

---

## 2. Schema / report inspection findings

| Area | Exists | Notes |
|------|--------|-------|
| Vehicles.Status | yes | Available=1, OnTrip=2, Maintenance=3, Retired=4, Draft=5 |
| VehicleCurrentLocation.LastUpdate | yes | No TenantId — join Vehicles |
| GpsDevices / GpsDeviceId | yes | Coverage = device assigned |
| FuelLogs.TotalCost / Liters / FuelDate | yes | Tenant + soft-delete |
| Maintenance work-orders | yes | Due via Status / NextDueDate; tenant via Vehicles |
| VehicleMaintenanceSchedules | yes | Separate from work-orders (SmartOps) |
| Trips.TripDate + Status | yes | Completed=9, Cancelled=10 |
| Bookings.PickupTime + Status | yes | Phase 4 / Phase 5 aligned |
| Payments.Amount Status=Paid(3) | yes | **No** BranchId / VehicleId / CustomerId on Payments |
| Revenue by vehicle/customer/branch | indirect | Payments → Bookings → Vehicle / Customer / Vehicles.BranchId |
| GpsFleetStatusSnapshots | yes | Preferred utilization % when present |
| GpsTrips | yes | **No** TenantId in base schema — join Vehicles |
| Branches.TimeZone | exists | **Not** used in analytics (all UTC) |
| Fleet health % | two formulas | AI `FleetHealthService` vs `GpsFleetHealthAssessor` — **not** exposed in Analytics |

### Reused ERP logic

- Fleet `get_maintenance_due` window semantics → `get_maintenance_kpi_summary`
- Fleet `get_fleet_status` stale minutes pattern → `get_gps_coverage_summary`
- `ReportRepository` vehicle/customer join pattern → `get_revenue_breakdown`
- Backend `GetVehicleUtilizationSummary` snapshot formula → preferred utilization path
- Phase 4 `get_vehicle_count` / `get_booking_summary` / `get_fuel_expenses` remain aliases

---

## 3. Tools implemented (contracts)

| Tool | Inputs | Outputs (structured) |
|------|--------|---------------------|
| `get_analytics_domain` | (none) | Catalog, sources, **unavailable** metrics, output contract |
| `get_fleet_inventory_summary` | `tenantId` | metrics: total/active/inactive + byStatus; point-in-time UTC |
| `get_gps_coverage_summary` | `tenantId`, `staleMinutes?` | withDevice, fresh, stale, neverSeen, coverage %; filters |
| `get_fuel_cost_summary` | `tenantId`, `dateFrom?`, `dateTo?`, `limit?`, `vehicleId?` | SQL `grandTotal`/`totalLiters` + TOP N `vehicles`; warnings if truncated |
| `get_maintenance_kpi_summary` | `tenantId`, `daysAhead?` | work-order overdue/due counts + schedule overdue/dueSoon |
| `get_trip_statistics` | `tenantId`, period | trips completed/pending/inProgress/cancelled/failed + bookings (PickupTime) |
| `get_booking_analytics` | `tenantId`, period | booking totals + byStatus + completion % |
| `get_revenue_summary` | `tenantId` | daily / weekly / monthly paid totals (UTC) |
| `get_revenue_breakdown` | `tenantId`, `groupBy` date\|branch\|vehicle\|customer, period, `limit?` | `totalPaid` (full SUM) + breakdown rows |
| `get_period_comparison` | `tenantId`, `mode` wow\|mom, `asOf?` | current/previous/delta/delta% for revenue, trips, bookings, fuel |
| `get_vehicle_utilization` | `tenantId`, period, `limit?` | snapshot util % if available else OnTrip share + GpsTrips ranking |
| `get_driver_performance` | `tenantId`, period, `limit?` | drivers ranked by completed Trips |

All tools: `ok`, `tenantId`, `filters` / `period`, `metrics[]`, `units` / `currency`, `warnings[]`, `empty`, `note`. TenantId injected by bridge.

---

## 4. KPI definition table

| Metric | Business definition | SQL / source | Filters / exclusions | Date / TZ | Currency / units | Null / missing | % denominator | Freshness |
|--------|---------------------|--------------|----------------------|-----------|------------------|----------------|---------------|-----------|
| vehicles_total | Non-deleted vehicles | `Vehicles` COUNT | `IsDeleted=0` | point-in-time UTC | vehicles | — | — | live |
| vehicles_active | Available+OnTrip+Maintenance | Status IN (1,2,3) | soft-delete | UTC | vehicles | — | — | live |
| vehicles_inactive | Retired+Draft | Status IN (4,5) | soft-delete | UTC | vehicles | — | — | live |
| with_gps_device | GpsDeviceId set | Vehicles | exclude Draft | UTC | vehicles | null device = no device | — | live |
| fresh_locations | LastUpdate within staleMinutes | VehicleCurrentLocation | exclude Draft | UTC vs GETUTCDATE() | vehicles | no row → not fresh | fleet_total excl Draft | live |
| stale_locations | LastUpdate older than threshold | same | exclude Draft | UTC | vehicles | — | fleet_total | live |
| device_coverage_percent | Devices / fleet | same | exclude Draft | UTC | % | — | fleet_total | live |
| fresh_reporting_percent | Fresh / fleet | same | exclude Draft | UTC | % | — | fleet_total | live |
| fuel_cost_total | Sum fuel spend | `SUM(FuelLogs.TotalCost)` | IsDeleted=0, period FuelDate | period UTC | tenant_currency | null cost → 0 in SUM | — | as of query |
| fuel_liters_total | Sum liters | `SUM(Liters)` | same | UTC | liters | null → 0 | — | as of query |
| work_orders_overdue | NextDueDate &lt; now | Maintenance⋈Vehicles | IsDeleted=0 | UTC | rows | null NextDueDate not overdue | — | live |
| work_orders_due_or_open | Due window / open status | same as Fleet due | daysAhead | UTC | rows | — | — | live |
| schedules_overdue / due_soon | Schedule NextDueDate | VehicleMaintenanceSchedules | IsActive, IsDeleted | UTC | rows | table missing → warning | — | live |
| trips_* | Status buckets | Trips | IsDeleted=0, TripDate | period UTC | trips | — | — | as of query |
| bookings_* | Status rollup | Bookings | IsDeleted=0, **PickupTime** | period UTC | bookings | — | bookings_total for completion % | as of query |
| revenue_* | Paid payments | Payments Status=3 | IsDeleted=0 | PaymentDate UTC | tenant_currency | null Amount → 0 | — | as of query |
| revenue by branch/vehicle/customer | Paid via booking links | Payments→Bookings→… | Paid only | PaymentDate UTC | tenant_currency | Unassigned when FK null | totalPaid not sum of truncated rows | as of query |
| utilization_rate_percent | Moving slots / available slots | GpsFleetStatusSnapshots preferred | period SnapshotAt | UTC | % | no snapshots → OnTrip/fleet fallback + warning | SUM(TotalVehicles) or fleet total | period / live |
| driver completed trips | Completed Trips per driver | Trips⋈Drivers | DriverId NOT NULL | TripDate UTC | trips | no trips → empty | — | as of query |
| WoW / MoM deltas | Current vs prior window | SQL per metric | IsDeleted=0 | UTC | mixed | prev=0 & curr&gt;0 → delta% **null** | previous period value | as of query |
| **fleet_health_percent** | — | — | — | — | — | **UNAVAILABLE** | — | pending agreement |

Full narrative definitions: this file §4 + MCP `get_analytics_domain.unavailable`.

---

## 5. Unavailable metrics + dependencies

| Metric | Dependency / blocker |
|--------|----------------------|
| **fleet_health_percent** | Agree single formula (AI weighted blend vs GpsFleetHealthAssessor average). Do not expose until then. |
| driver_hours_utilization_% | No on-duty hours ledger |
| invoice / AR aging | Optional CustomerInvoices; not on Analytics path |
| branch TZ-local calendars | Branches.TimeZone unused — all KPIs UTC |
| Payments native branch/vehicle/customer | Must join Bookings (documented) |

---

## 6. Tests

### SheikhGo-MCP
- `tests/phase5-analytics.test.ts` — fuel grandTotal ≠ TOP N sum; inventory; GPS %; maintenance; trip/booking PickupTime; revenue truncation; WoW null %; MoM boundaries; empty; tenant params
- `tests/analytics-status-maps.test.ts` — Phase 5 catalog + unavailable fleet health

### Sheikh-Travel-System
- `McpAllowlistTests` — new Phase 5 tool names allowlisted
- Keyword `GetFuelCostSummary` — fleet SQL totals (parity with MCP)

---

## 7. Files changed

### SheikhGo-MCP
- `src/analytics/analytics-queries.ts`
- `src/analytics/status-maps.ts`
- `src/mcp/tools.ts`
- `scripts/export-tool-catalog.mjs`
- `package.json` → `0.15.0`
- `tests/phase5-analytics.test.ts`
- `tests/analytics-status-maps.test.ts`
- `src/knowledge/agent-playbook.md` (Analytics section)

### Sheikh-Travel-System
- `Backend/.../Mcp/McpAllowlist.cs`
- `Backend/.../Mcp/McpDatabaseRecordRouter.cs`
- `Backend/.../Mcp/SheikhGoMcpBridge.cs`
- `Backend/.../Ai/AiMcpToolCallingLoop.cs`
- `Backend/.../Tools/AssistantReportingTools.cs` (fuel grandTotal fix)
- `Backend/.../Tests/AI/McpAllowlistTests.cs`
- `docs/AI-MCP-Phase5-Analytics.md` (this file)
- ERP `mcp-catalog.json` assets (via export)

---

## 8. Commands

```bash
cd /Users/alirazatahir/Projects/SheikhGo-MCP && npm run typecheck && npm test && npm run export:catalog && npm run build

dotnet test /Users/alirazatahir/Projects/Sheikh-Travel-System/Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj \
  --filter "FullyQualifiedName~McpAllowlist|FullyQualifiedName~McpDatabaseRecordRouter|FullyQualifiedName~AiMcpToolCalling"
```

---

## 9. STOP

Phase 5 Analytics complete for Ali review. **Do not start Phase 6** until approved.

**Follow-ups for Ali/product**
1. Choose fleet-health formula (or keep unavailable).
2. Confirm active/inactive vehicle definition (Status 1–3 vs “not Retired only”).
3. Confirm default GPS stale minutes (30 vs GpsSettings / 15 AI health).
4. Optional: unify backend `GetBestDriverPerformance` (Bookings) with MCP trips ranking.
