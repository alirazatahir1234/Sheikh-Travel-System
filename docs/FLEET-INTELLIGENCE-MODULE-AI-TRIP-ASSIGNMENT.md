# Fleet Intelligence Module 4 — AI Trip Assignment

**Document type:** Planning + implementation status  
**Module:** AI-assisted / Smart Trip Assignment (Fleet Intelligence #4)  
**Primary requirements source:** Ali’s Module 4 prompt (verbatim business + architecture intent below)  
**Cross-check:** Codebase audit + `docs/FLEET-INTELLIGENCE-IMPLEMENTATION-PLAN.md` (28 Oct 2026)  
**Repo:** `/Users/alirazatahir/Projects/Sheikh-Travel-System`  
**Status:** **Implemented (hardening ship)** — extended existing Smart Trip Assignment per Ali defaults (28 Oct 2026)  
**Audited:** 28 Oct 2026  

### Implementation status (shipped vs deferred)

| Area | Status |
|------|--------|
| Product name **Smart Trip Assignment**; net10.0 | Shipped (docs + UI copy) |
| Create-time auto-assign disabled | **Shipped** — removed from `CreateTripCommand` |
| Suggest-only mode (staff confirm) | **Shipped** — `POST …/confirm`; UI Confirm; `auto` blocked |
| Hard filters: OnTrip, no vehicle, capacity, maintenance, shift ≥12h, schedule conflict, EV 15% | **Shipped** |
| Vehicle type hard filter | **Shipped when `RequiredVehicleType` set** — trips do not yet store required type (always null); filter + tests ready |
| Soft scores: performance/safety, cost, utilization + renormalized weights | **Shipped** |
| Suggestions return excluded list + reason counts | **Shipped** |
| ERP board `/trips/smart-assignment` | **Shipped** — mode badge, excluded panel, confirm CTA |
| Shadow mode support | **Shipped (design)** — `SmartAssignmentPolicy` + Shadow outcome logging when Shadow mode / force flag; ActiveMode stays SuggestOnly |
| AutoApply tenant flag | Deferred — off by default; enable after ~2 weeks shadow |
| Configurable tenant weights / EV buffer | Deferred |
| LLM explain | Deferred (not in scope) |
| Override learning / metrics dashboard | Deferred |
| Full Document Tracker wiring | Deferred (license + verification remain) |

---

## 0. Ali’s Module 4 prompt (primary — condensed)

> Implement AI-assisted trip assignment using Angular, ASP.NET Core .NET 8, SQL Server, GPS/Traccar.  
> **Business:** Recommend the most suitable available driver and vehicle respecting operational constraints.  
> **Factors:** Driver availability/shift; vehicle availability/capacity; vehicle type/trip requirements; pickup location/proximity; delivery deadline/ETA; qualifications/document validity; vehicle maintenance; driver performance/safety history; cost/route suitability/utilization.  
> **Architecture:** Transparent constraint-based scoring/optimization. Existing AI may NL-interpret or explain recommendations — **do not** rely on LLM alone for unvalidated assignments. Separate **mandatory constraints** vs **optional preferences**. Never assign unavailable/noncompliant because score is high.  
> **Workflow:** Retrieve trip requirements → eligible drivers/vehicles → filter mandatory violations → score/rank → explain reasons → authorized staff review/confirm → audit. Support reassignment + conflict detection where the system permits. **Never auto-confirm without explicit authorization.**

**Planning override:** This document captures FR, design, reuse, gaps, and order. APIs/screens/tests are outlined only — **no coding in this pass.**

---

## 1. Executive summary

SheikhGo already has a **working Smart Trip Assignment MVP**: transparent weighted scoring, Google Routes (with Haversine fallback), EV range hard-gate, eligibility via `DriverAssignmentGuard`, ERP board at `/trips/smart-assignment`, decision audit table, driver reject → re-offer, and trip create hook that auto-assigns when no driver is set.

That MVP **partially matches** Ali’s vision (transparent scoring, explainability, GPS/Routes, some compliance gates) but **diverges** on several product rules Ali stated as mandatory:

| Ali rule | Current reality |
|----------|-----------------|
| Never auto-confirm without explicit authorization | **Differs** — `CreateTripCommand` already calls `AutoAssignAsync(..., fromTripCreation: true)` when `DriverId` is empty |
| Mandatory vs optional preferences (never assign noncompliant due to high score) | **Partial** — eligibility + EV range are hard filters; capacity, vehicle type, maintenance, full docs, shift windows are **not** hard constraints yet |
| Full factor set (capacity, type, maintenance, performance/safety, cost/utilization) | **Partial** — scorer has rating, proximity, fatigue, fuel/battery, on-time pickup, traffic only |
| Staff review/confirm before apply | **Partial** — UI can suggest then “Assign best”; create path skips review |
| LLM only for explain / NL | **Aligned** — DeepInfra is **not** the assignment scorer |
| Stack “.NET 8” | **Differs** — backend targets **net10.0** (not net8) |

**Recommendation:** Position and ship as **Smart Trip Assignment** (transparent weighted + constraints), with optional “AI-assisted” marketing only if UI shows factor breakdown. Keep `TripAssignmentScorer` as source of truth; optional DeepInfra “explain these factors” later. Do **not** invent an LLM dispatcher.

**Architecture stance:** Stay on modular monolith. Harden constraints → shadow metrics → tenant-gated auto-apply → learning from overrides only after data exists.

---

## 2. Stack assumptions (verified)

| Assumption | Verified? | Evidence |
|------------|-----------|----------|
| Angular ERP | **Yes** | `Frontend/sheikhgo-erp` — Angular ^20.3; route `/trips/smart-assignment` |
| ASP.NET Core | **Yes** | `Backend/SheikhTravelSystem.API` |
| .NET version | **net10.0** (Ali prompt said .NET 8) | `SheikhTravelSystem.API.csproj` → `<TargetFramework>net10.0</TargetFramework>` |
| SQL Server + Dapper | **Yes** | `TripSmartAssignmentRepository`, migrations via `DatabaseMigrationRegistry` |
| Traccar / GPS | **Yes** | `VehicleCurrentLocation`, Traccar sync; proximity from live location |
| Google Routes | **Yes** | `IGoogleRoutesService` / `GoogleRoutesService`; `MapTrafficRegions` (UAE, PK, SA) |
| DeepInfra / chat AI | **Yes** (separate) | Used for assistant — **not** trip scoring today |

---

## 3. Naming & positioning (honest)

| Term | Use when |
|------|----------|
| **Smart Trip Assignment** (preferred product name) | ERP menus, APIs already named `smart-assignment`, docs |
| **Constraint-based / weighted scoring** | Engineering, sales engineering, RFPs |
| **AI-assisted** | Only if explainability + optional NL explanation; never imply ML/LLM picks the driver alone |
| **Avoid** | “AI Trip Assignment” without qualifier; “ML dispatcher”; “AI auto-assigns your fleet” |

Website already markets explainable weighted assignment (`home.smartAssignment.*`). Align ERP copy with that honesty.

---

## 4. Business problem & value

**Problem:** Dispatchers manually pick drivers under time pressure; assignments can ignore fatigue, fuel/range, proximity, compliance, and capacity — causing late pickups, double-booking risk, and fairness complaints.

**Value:** Faster dispatch; consistent policy; auditable “why this driver”; fewer noncompliant offers; measurable override rate and time-to-assign.

**Buyers:** Ops / dispatch leads; fleet managers; enterprise RFPs asking for “AI dispatch” (sell explainable smart assign).

**Pricing suggestion (from suite plan):** Dispatch / Smart Ops tier add-on or included with GPS.

**KPIs:** Time-to-assign; suggestion→confirm rate; override rate + reasons; on-time pickup; reject/re-offer count; % assignments with full GPS + Routes (vs Haversine); hard-constraint filter rate (how often candidates are eliminated).

---

## 5. Functional requirements

### 5.1 Core FR (from Ali — must satisfy)

| ID | Requirement |
|----|-------------|
| FR-1 | Given a trip, system retrieves trip requirements (time window, pickup/drop coords, passengers, type/priority, etc.). |
| FR-2 | System builds eligible driver+vehicle pairs from available, assignable resources. |
| FR-3 | **Mandatory constraints** eliminate candidates before scoring (availability, compliance, capacity, type if required, maintenance blocks, schedule conflicts). High score never overrides a fail. |
| FR-4 | Remaining candidates are **scored/ranked** with transparent named factors and weights. |
| FR-5 | UI/API returns **explanations** (factor list + details), not a black-box score only. |
| FR-6 | **Authorized staff** review and confirm before a recommendation becomes the active assignment (default). Auto-apply only if Ali later enables a tenant flag. |
| FR-7 | All proposals, confirms, overrides, rejects, and re-offers are **audited**. |
| FR-8 | Support **reassignment** and **conflict detection** where repository/lifecycle already permits. |
| FR-9 | LLM (if used) may explain or NL-summarize rankings — **never** be the sole authority for assignment. |

### 5.2 Soft / later FR

| ID | Requirement |
|----|-------------|
| FR-10 | Tenant-configurable weights and EV range buffer. |
| FR-11 | Shadow mode: log “would assign X” without writing trip driver for N weeks. |
| FR-12 | Cost / utilization factors once fuel cost + utilization data quality is trusted. |
| FR-13 | Learn from override reasons (analytics only first; no closed-loop ML without labeled volume). |
| FR-14 | Optional DeepInfra “explain factors in Arabic/English” for sales demos. |

### 5.3 Non-goals (this module)

- Replacing fleet **vehicle↔driver** long-term `AssignmentHistory` board with trip smart assign (different product surface).
- Full OCPI / charger booking (EV module).
- SOS nearest-unit auto-dispatch (separate module; uses `GeoDistance.AutoAssignMaxKm`).
- True ML ranking models without datasets.

---

## 6. User stories

1. **As a dispatcher**, I open Smart Assignment, select an unassigned trip, and see ranked drivers with factor bars so I understand why #1 was recommended.  
2. **As a dispatcher**, I confirm the top candidate (or override with a reason) so the trip gets driver + linked vehicle.  
3. **As an ops manager**, I require that expired license / unverified / suspended drivers never appear as assignable even if nearby.  
4. **As a fleet manager**, I expect vehicles under capacity or wrong type for the trip’s passenger/vehicle needs to be filtered out.  
5. **As a dispatcher**, when a driver rejects a smart offer, the system offers the next ranked eligible driver (or notifies dispatch if none remain).  
6. **As compliance**, I can pull decision audit rows showing score breakdown JSON and outcome (Proposed / Overridden / Rejected / Superseded).  
7. **As Ali / product**, I can turn off create-time auto-assign so no trip is confirmed without a human (or explicit tenant policy).  
8. **As an EV fleet ops user**, electric vehicles with insufficient estimated range for the trip distance are excluded.

---

## 7. Constraint model (Ali architecture)

### 7.1 Mandatory constraints (hard filters — fail → exclude)

| Constraint | Ali intent | Today | Gap |
|------------|------------|-------|-----|
| Driver available / not suspended-leave | Yes | `IsActive` + status Available/OnTrip; guard blocks Suspended/OnLeave | **OnTrip still eligible** in SQL — risky for concurrent trips; rely on later conflict check |
| Shift / hours | Yes | Fatigue **scored** from `DriverAttendance`, not hard max hours | Hard max daily hours / HOS policy missing |
| Vehicle available | Yes | Active `AssignmentHistory` vehicle link; vehicle trip conflict on assign | Candidates without vehicle still ranked (“No linked vehicle”) |
| Capacity ≥ passengers | Yes | `Vehicles.SeatingCapacity`, `Trips.PassengerCount` exist | **Not joined/filtered** in smart assign |
| Vehicle type / trip requirements | Yes | `VehicleType`, trip type/priority exist elsewhere | **Not filtered** in smart assign |
| Document / license validity | Yes | License expiry + verification (`Verified` / reject `ExpiredDocs`) | Broader Document Tracker / vehicle docs **not** wired |
| Maintenance block | Yes | Predictive / WO modules exist | **Not** a smart-assign hard gate |
| Schedule conflict | Yes | `TripRepository.AssignDriverAsync` / `AssignVehicleAsync` conflict SQL | Filter happens at apply, not always at suggest-time |
| EV range buffer | Implied | `HasSufficientEvRange` (~15% buffer) | Buffer not tenant-configurable |

### 7.2 Optional preferences (scored — soft)

| Preference | Weight today | Notes |
|------------|--------------|-------|
| Driver rating | 0.22 | `Drivers.Rating` |
| Proximity to pickup | 0.18 | GPS / Routes |
| Fatigue (hours today) | 0.15 | Soft score; not hard fail |
| Fuel / EV battery % | 0.15 | Neutral if missing telemetry |
| On-time pickup (ETA vs planned) | 0.15 | |
| Route / traffic | 0.15 | Google Routes vs Haversine |
| Performance / safety history | — | Driver Performance module exists; **not** in scorer |
| Cost / utilization | — | **Missing** |
| Maintenance remaining life | — | Soft preference possible later |

Weights are constants in `TripAssignmentScorer` (sum = 1.0). Ali should officially approve or revise.

---

## 8. Target workflow (Ali) vs current workflow

```
Ali (desired):
  Trip requirements
    → Candidates (driver+vehicle)
    → Mandatory filter
    → Score / rank + explain
    → Staff confirm
    → Audit
    → (Optional) reassign / conflict

Current:
  Trip (create may AutoAssign immediately)  ← policy conflict with Ali
    → Candidates (drivers ± active vehicle)
    → Soft eligibility (guard) + EV range
    → Score / rank + factors in UI
    → Manual “Assign best” / override OR already auto on create
    → TripSmartAssignmentDecisions audit
    → Driver reject → clear → next ranked
```

---

## 9. What already exists (reuse)

### 9.1 Backend

| Asset | Path / symbol |
|-------|----------------|
| Scorer | `Backend/SheikhTravelSystem.Application/Features/Trips/SmartAssignment/TripAssignmentScorer.cs` |
| DTOs / commands | `…/SmartAssignment/TripSmartAssignmentDtos.cs`, `TripSmartAssignmentCommands.cs` |
| Service contract | `ITripSmartAssignmentService` |
| Service impl | `Backend/SheikhTravelSystem.Infrastructure/Services/Trips/TripSmartAssignmentService.cs` |
| Repository | `TripSmartAssignmentRepository.cs` + `ITripSmartAssignmentRepository` |
| Migration | `TripSmartAssignmentMigration.cs` → `dbo.TripSmartAssignmentDecisions` |
| DI | `DependencyInjection.cs` scoped service + repository |
| API | `TripsController`: `GET …/suggestions`, `POST …/auto`, `POST …/override`, `GET …/decisions`, `GET …/smart-assignment/recent` |
| Permissions | Class `[RequirePermission(TripView)]`; auto/override `[TripAssign]` |
| Create hook | `CreateTripCommandHandler` → `AutoAssignAsync(..., fromTripCreation: true)` |
| Reject re-offer | `DriverTripLifecycleCommands` → `TryReassignAfterDriverRejectAsync` |
| Eligibility | `DriverAssignmentGuard.EnsureAssignableForVehicleTrip` |
| Manual assign | `AssignTripDriverCommand` / `AssignTripVehicleCommand` + conflict checks in `TripRepository` |
| Routes | `IGoogleRoutesService`, `GoogleRoutesService`, `MapTrafficRegions` |
| Docs | `Backend/docs/TRIP_SMART_ASSIGNMENT.md` |
| Tests | `Backend/SheikhTravelSystem.Tests/Trips/TripAssignmentScorerTests.cs` |
| Related (not same) | Fleet `Assignments` board (`AssignmentRepository`, ERP `assignment-board`); booking assign commands; SOS `SosAutoDispatchService` |

### 9.2 Frontend

| Asset | Path |
|-------|------|
| Page | `Frontend/sheikhgo-erp/src/app/modules/trips/smart-assignment/*` |
| Service / models | `core/services/trip-smart-assignment.service.ts`, `core/models/trip-smart-assignment.model.ts` |
| Route | `trips.module.ts` → `smart-assignment` |
| Nav | `menu-config.ts` → “Smart Assignment”; trips layout tab |
| Factor UI | Candidate cards with weight %, bars, detail text (already explainable) |
| Marketing | Website `home.smartAssignment.*` |

### 9.3 API surface (existing)

- `GET /api/trips/{id}/smart-assignment/suggestions`
- `POST /api/trips/{id}/smart-assignment/auto`
- `POST /api/trips/{id}/smart-assignment/override`
- `GET /api/trips/{id}/smart-assignment/decisions`
- `GET /api/trips/smart-assignment/recent`

### 9.4 Persistence

`TripSmartAssignmentDecisions`: TenantId, TripId, DriverId, VehicleId, Rank, TotalScore, ScoreBreakdownJson, RouteId, DistanceKm, EtaMinutes, TrafficSource, Outcome, RejectionReason, CreatedBy, CreatedAtUtc.

---

## 10. Gaps to build (vs Ali’s prompt)

Priority ordered by product risk / Ali alignment:

1. **Policy: create-time auto-assign** — Conflicts with “never auto-confirm without explicit authorization.” Need tenant flag default **off** or remove create hook until Ali approves.  
2. **Hard constraints incomplete** — Capacity, vehicle type, maintenance hold, shift/HOS max, stricter “not OnTrip / no schedule overlap” at **suggestion** time.  
3. **Trip context incomplete for scoring** — Suggestions load coords/planned start; do not pass `PassengerCount`, required vehicle type, delivery deadline beyond planned start, or maintenance status into filter.  
4. **Confirm ≠ auto** — Introduce explicit `confirm` / suggestion-only mode; keep `auto` for opted-in tenants.  
5. **Driver performance / safety** — Reuse `DriverScoreCalculator` / ranking as optional preference; not wired.  
6. **Cost & utilization** — No factors; defer until data quality OK.  
7. **Document breadth** — License + verification only; vehicle/driver Document Tracker expiry not in candidate SQL.  
8. **Configurable weights / EV buffer** — Hardcoded today.  
9. **Shadow-mode metrics dashboard** — Called out in suite plan; not built.  
10. **Tests** — Scorer unit tests only; missing constraint filter tests, conflict-at-suggest tests, create-policy tests.  
11. **LLM explain (optional)** — Not started; must remain non-authoritative.  
12. **Stack wording** — Docs/prompts saying “.NET 8” should say **net10.0** for this repo.

---

## 11. Technical approach (when implementation is approved)

### 11.1 Principles

1. **Filter then score** — Expand `IsEligible` / pre-score gates; never fold mandatory fails into soft scores.  
2. **Single scorer** — Keep `TripAssignmentScorer` as ranking authority; add factors carefully with weight renormalization and Ali sign-off.  
3. **Modes** — `SuggestOnly` (default) | `AutoApply` (tenant flag) | `Shadow` (log only).  
4. **Apply path** — Continue using `AssignTripDriverCommand` / `AssignTripVehicleCommand` so conflict detection stays centralized.  
5. **Explain** — Always return `TripAssignmentScoreFactorDto` list; UI already renders. Optional AI summary = text over factors.  
6. **No new microservice / ML platform.**

### 11.2 Suggested scoring outline (target state)

**Hard filters (examples):** verified + license OK; not suspended/leave; no overlapping trip; vehicle present; seating ≥ passengers; type match if trip requires; no critical maintenance block; EV range ≥ trip×buffer; optional max hours.

**Soft scores (evolve current 6):** keep existing six until Ali approves additions; candidate additions: safety/performance (from driver score), utilization (recent km / idle), cost proxy (fuel efficiency or estimated route cost). Always document weight sum = 1.0.

### 11.3 Data sources to wire

| Need | Source candidates |
|------|-------------------|
| Location | `VehicleCurrentLocation` via active `AssignmentHistory` |
| Fatigue / shift | `DriverAttendance` (+ future shift tables if introduced) |
| Capacity | `Vehicles.SeatingCapacity` vs `Trips.PassengerCount` |
| Type | `Vehicles.VehicleType` vs trip/booking requirement fields |
| Docs | `Drivers.LicenseExpiryDate`, `VerificationStatus`; Document Tracker tables |
| Maintenance | Predictive flags / open WO critical status |
| Performance | `DriverScoreCalculator` / analytics queries |
| Traffic | Google Routes; fallback Haversine |

### 11.4 UI approach

- Harden existing Smart Assignment page (factor panel already good).  
- Add mode badge: Suggest / Auto / Shadow.  
- Show **excluded** count with top exclusion reasons (capacity, docs, range) — builds trust.  
- Separate “Confirm recommendation” from create-trip.  
- Optional: trip detail “Suggest drivers” entry point.

### 11.5 Permissions

Reuse `OperationsPermissions.TripView` (read suggestions/decisions) and `TripAssign` (confirm/auto/override). Do not weaken.

### 11.6 Testing outline (when coding)

- Extend scorer tests for new factors.  
- Unit: mandatory filter matrix (capacity fail, expired license, EV range).  
- Integration: suggest does not write trip; confirm writes decision + assignment; conflict returns clear error.  
- Policy: create-trip does not auto-assign when flag off.  
- Regression: driver reject re-offer still works.

### 11.7 Rollback

Disable auto-apply flag; keep suggestions + manual assign; decisions table retained for audit.

---

## 12. Recommended implementation order

| Phase | Focus | Outcome | Depends on Ali |
|-------|--------|---------|----------------|
| **P0** | Policy & honesty | Disable or gate create-time auto-assign; rename copy to Smart/constraint-based; document net10 | Auto-assign policy |
| **P1** | Mandatory filters | Capacity, type (if required), schedule overlap at suggest-time, tighten OnTrip rules, maintenance critical hold | Which constraints are hard |
| **P2** | Suggest vs confirm UX | Explicit confirm; excluded-reasons panel; shadow logging optional | Default mode |
| **P3** | Soft factors + config | Wire driver performance soft score; tenant weights + EV buffer | Weight table approval |
| **P4** | Ops metrics | Override rate, time-to-assign, Haversine % dashboard | — |
| **P5** | Optional NL explain | DeepInfra summarizes factor JSON only | Want LLM explain? |
| **P6** | Cost/utilization + learning | Only after data quality | Pricing of factor |

Do **not** start P5/P6 before P0–P2.

---

## 13. Alignment vs audit (Ali prompt ↔ codebase)

### Already exists (strong reuse)

- Transparent weighted scoring with named factors returned to API/UI.  
- GPS proximity + Google Routes / Haversine fallback.  
- Fatigue hours and fuel/battery as soft factors.  
- On-time pickup vs ETA soft factor.  
- EV range hard skip (~1.15× trip distance).  
- License + verification eligibility via `DriverAssignmentGuard`.  
- Suggest / auto / override / decisions APIs + ERP board with factor bars.  
- Audit table `TripSmartAssignmentDecisions` with ScoreBreakdownJson.  
- Driver reject → re-offer next ranked.  
- Trip-level driver/vehicle conflict checks on assign.  
- LLM **not** used for ranking (aligned with Ali’s “do not rely on LLM alone”).

### Partial

- “Available driver + vehicle” — prefers active vehicle assignment; can still suggest drivers with no vehicle.  
- Compliance — license/verification only, not full document set.  
- Delivery window — planned start vs ETA only (no separate delivery SLA window model).  
- Staff review — available on board, but create path auto-applies.  
- Reassignment — reject re-offer yes; general mid-trip reassign UX not a dedicated smart flow.  
- Architecture “optimization” — ranked heuristic, not OR-Tools / solver.

### Missing (relative to Ali’s factor list)

- Vehicle **capacity** filter.  
- Vehicle **type / trip requirements** matching.  
- Vehicle **maintenance** as constraint.  
- Driver **performance/safety history** in scorer (module exists separately).  
- **Cost / route suitability / utilization** factors.  
- Explicit **shift** windows (only attendance hours).  
- Tenant-configurable weights / buffers.  
- Shadow mode + confirm-first default policy.  
- Suggest-time exclusion reasons API.  
- LLM NL explanation layer (optional).

### Differs / conflicts

| Topic | Ali prompt | Audit finding |
|-------|------------|---------------|
| Runtime | “.NET 8” | **net10.0** |
| Auto-confirm | Never without explicit authorization | Create-trip **auto-assigns** when no driver |
| Naming | “AI-assisted trip assignment” | Code/UI: **Smart Assignment**; heuristic not generative AI |
| Optimization | Constraint-based scoring/optimization | Weighted sum + hard EV/eligibility; no solver |
| Authorization gate | Staff confirm before assign | `POST …/auto` and create hook can assign without a separate “confirm suggestion” entity |

---

## 14. Questions needing Ali’s decisions

1. **Create-time auto-assign:** Keep, disable globally, or tenant flag default **off**? (Prompt says never auto-confirm without authorization.)  
2. **Default mode:** Suggest-only until confirm, vs allow `TripAssign` users to one-click “Assign best”?  
3. **OnTrip drivers:** Exclude from suggestions entirely, or allow if no schedule overlap?  
4. **Drivers without linked vehicle:** Exclude, or suggest driver-only and force vehicle pick on confirm?  
5. **Hard capacity / vehicle type:** Always enforce when `PassengerCount` / type present?  
6. **Maintenance:** Block on open critical WO / overdue predictive only, or soft penalty?  
7. **Official weights:** Approve current 0.22 / 0.18 / 0.15×4 or provide new table?  
8. **EV buffer:** Keep 15% or make configurable (e.g. 10–30%)?  
9. **Product name:** “Smart Trip Assignment” vs market as “AI-assisted”?  
10. **LLM explain:** Want Arabic/English DeepInfra summary in MVP+, or never?  
11. **Cost/utilization:** In scope for v1 hardening or explicitly Phase 6?  
12. **Shadow period:** Require 2 weeks shadow before any tenant enables AutoApply?

---

## 15. Acceptance criteria (for a future implementation PR — not this pass)

- [ ] Mandatory failures never appear in ranked “eligible” list (or appear only in a separate “excluded” list with reason).  
- [ ] Factor weights for soft scores documented and sum to 1.0.  
- [ ] UI shows factor breakdown for every suggestion.  
- [ ] Confirm/apply writes `TripSmartAssignmentDecisions` with outcome.  
- [ ] Override requires reason (or strongly prompts) and logs Overridden.  
- [ ] Create-trip behavior matches Ali’s authorization policy.  
- [ ] Conflict detection still enforced via existing assign commands.  
- [ ] No LLM call required for ranking to succeed.  
- [ ] Unit tests cover new hard filters + policy flag.  
- [ ] Copy does not claim ML/LLM assignment.

---

## 16. Related documents

- `docs/FLEET-INTELLIGENCE-IMPLEMENTATION-PLAN.md` — suite plan §A.4, §E.4, Phase 3 Dispatch intelligence  
- `Backend/docs/TRIP_SMART_ASSIGNMENT.md` — engineer-facing signal/API notes  
- Driver Performance module plan (when present): soft-score input candidate  

---

## 17. Out of scope for this planning pass

No code, schema, migrations, config, or production data were modified. Implementation waits on Ali’s answers in §14 and an explicit build request.
