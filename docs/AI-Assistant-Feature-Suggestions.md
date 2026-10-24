# AI Assistant — Feature Suggestions

Proposed capabilities for the SheikhGo AI assistant. These are suggestions for review—not yet built unless already covered by Phase 2 tools.

---

## Hard constraint — LLM providers

**Do not use OpenAI, Azure OpenAI, or Azure AI** for AI Assistant work (including Phase 3 predictive features and any future chat/LLM integration).

| Allowed | Not allowed |
|---------|-------------|
| **Ollama** with **Qwen 3** (e.g. `qwen3:8b`; optional chat polish only) | OpenAI, Azure OpenAI, Azure AI |
| Rules / stats for predictions (no LLM required for Phase 3) | LLM-backed predictions |

Keep existing OpenAI UI stubs unless explicitly asked to remove them. This constraint is documentation only—it does not change application code by itself.

---

## 1. Revenue analytics — DONE (Batch 1)

Answer daily, weekly, and monthly revenue totals for the current tenant (or selected period).
Tool: `GetRevenueAnalytics` (paid `Payments` only).

## 2. Fuel usage analysis — DONE (Batch 1)

Identify which vehicle consumed the most fuel over a given period, with optional totals and comparisons.
Tool: `GetFuelUsageTopVehicle` (ranked by liters from `FuelLogs`).

## 3. Driver performance — DONE (Batch 1)

Report the best-performing driver for today (or a chosen range), using trip, booking, or score metrics already available in the system.
Tool: `GetBestDriverPerformance` (completed bookings + booking revenue).

## 4. Trip and booking analytics — DONE (Batch 2)

Summarize trip volume, booking status mix, completion rates, and trends for a selected period.
Tool: `GetTripSummary` (extended with booking KPIs).

## 5. Cost per kilometer — DONE (Batch 2)

Compute and explain fleet or vehicle-level cost per kilometer (fuel, maintenance, and related expenses where data exists).
Tool: `GetCostPerKilometer` (fuel + maintenance / GPS distance; FleetExpenses excluded unless distance exists).

## 6. Vehicle utilization rate — DONE (Batch 2)

Show how much of the fleet (or a specific vehicle) is utilized versus idle/available over a period.
Tool: `GetVehicleUtilizationSummary` (fleet % from `GpsFleetStatusSnapshots` + GPS trip ranking).

## 7. Overspeed and geofence violation summary — DONE (Batch 2)

Summarize overspeed events and geofence breaches by vehicle, driver, severity, or time window.
Tool: `GetOverspeedGeofenceSummary`.

## 8. Maintenance due alerts — DONE (Batch 2)

Surface upcoming and overdue maintenance (aligned with Task 1 Smart Operations / smart alerts).
Tool: `GetVehicleMaintenanceSummary` (reads `SmartAlerts` MaintenanceDue + schedules; does not re-scan).

## 9. Idle time report — DONE (Batch 3)

Report idle time by vehicle or fleet—duration, locations if available, and notable outliers.
Tool: `GetIdleTimeReport` (snapshots + `idle_vehicle` alerts; Traccar stop fan-out not used).

## 10. SOS events — DONE (Batch 3)

List and summarize SOS / emergency events for a period, with status and related vehicle or driver context.
Tool: `GetSosEventsSummary`.

## 11. Expense tracking — DONE (Batch 3)

Answer questions about expenses—totals by category, vehicle, or period, and notable spikes.
Tool: `GetExpenseTracking` (fuel, maintenance, trip expenses, FleetExpenses).

## 12. Super-admin tenant comparison — DONE (Batch 3)

For platform / super-admin: compare key metrics across tenants (revenue, utilization, alerts, activity).
Tool: `GetTenantComparison` (RBAC: `IsPlatformSuperAdmin` only; denied otherwise).

## 13. Peak hours analysis — DONE (Batch 3)

Identify peak booking, trip, or dispatch hours so operations can plan staffing and fleet coverage.
Tool: `GetPeakHoursAnalysis` (UTC hour buckets).

---

## Phase 3 — Predictive Intelligence (rules/stats; no ML)

Independent of the LLM; assistant explains tool results only. No OpenAI/Azure.

| Tool | What it does |
|------|----------------|
| `GetMaintenanceRiskForecast` | Oil / tire / engine risk via `PredictiveMaintenanceHelper` + open Smart Ops `MaintenanceDue` context (does not re-raise alerts). |
| `GetTripDelayRisk` | Schedule-based late start / past expected end; stale GPS flagged separately; **no** Maps ETA. |
| `GetFuelAnomalyInvestigation` | Smart Ops `FuelAnomaly` + L/100 km vs fleet median when sample thresholds met; investigation wording only. |

---

*Batches 1–3 + Phase 3 Predictive Intelligence for `/ai/chat`.*
