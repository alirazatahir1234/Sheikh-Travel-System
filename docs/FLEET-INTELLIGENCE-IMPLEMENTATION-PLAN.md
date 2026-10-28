# SheikhGo Fleet Intelligence Suite — Implementation Plan

**Document type:** Planning only (no code, schema, config, or production data changes in this pass)  
**Audited:** 28 Oct 2026  
**Repo:** `/Users/alirazatahir/Projects/Sheikh-Travel-System`  
**Audience:** Ali (founder/PM), engineering, sales  
**Status:** Draft for business decision + phased hardening of **existing** scaffolds

---

## Executive summary

SheikhGo already has **substantial scaffolding for all 11 proposed Fleet Intelligence modules** inside the modular monolith (Angular ERP + ASP.NET Core **net10.0** + SQL Server + Traccar + DeepInfra/MCP). This is **not** a greenfield suite: the product work is mostly **maturity, data quality, ops UX, packaging, and honest positioning** (rule-based vs “AI/ML”).

| # | Module | Audit class | One-line reality |
|---|--------|-------------|------------------|
| 1 | Driver Performance Scoring | **Partially implemented** (strong MVP) | Rule-based Safety / Fuel / On-time scores + ERP page |
| 2 | Predictive Maintenance | **Partially implemented** | Interval/odometer rules — **not** ML failure prediction |
| 3 | Shareable Tracking Links | **Already implemented** (MVP+) | Token links + anonymous public page `/t/{token}` |
| 4 | AI Trip Assignment | **Hardened MVP** (Smart Trip Assignment) | Constraints + weighted scores; suggest→confirm; create auto-assign off |
| 5 | EV Fleet Support | **Partially implemented** | Dashboard, charging POIs, range-aware assignment hooks |
| 6 | Document Tracker | **Partially implemented** | Compliance docs CRUD + reminders hosted service |
| 7 | Anomaly Detection | **Partially implemented** | Rule evaluator on ingest (night / speed-drop / off-route) |
| 8 | Fuel Management | **Already implemented** (core) | Logs, efficiency, OBD telemetry panels |
| 9 | Fuel Fraud Detection | **Partially implemented** | Pump order vs OBD level — **blocked** without fuel-level devices |
| 10 | SOS Auto-Dispatch | **Partially implemented** | Nearest verified team + recovery partners |
| 11 | Predictive Accident Risk | **Partially implemented** | Rule risk 0–100; proactive scanner exists |

**Recommended first 3 (product + go-live):**  
1) **Shareable Tracking Links** (package & sell — already built)  
2) **Driver Performance Scoring** (tune weights, sub-scores, coaching flags)  
3) **Fuel Management + Fuel Fraud** as one commercial “Fuel Intelligence” offer (CRUD is ready; fraud gated on OBD)

**Architecture stance:** Stay on the **modular monolith**. Do **not** introduce microservices, a message broker, a second OLTP DB, or an ML platform unless labeled accident/fraud datasets and multi-tenant demand justify it later.

**Related recent work (relevant context):** GPS Alerts redesign / module alert catalog; global company + vehicle scope on live map (`GpsScopeFilterService`); Devices / tracker catalog (Teltonika / Queclink / Jimi); Driver Performance already in GPS nav; Smart Assignment + EV range note in ERP menus.

---

## Stack assumptions (verified)

| Assumption | Verified? | Evidence |
|------------|-----------|----------|
| Angular ERP | **Yes** | `Frontend/sheikhgo-erp` — Angular **^20.3** (`package.json`) |
| ASP.NET Core | **Yes** | `Backend/SheikhTravelSystem.API` |
| .NET version | **net10.0** (not net8) | All five `*.csproj` set `<TargetFramework>net10.0</TargetFramework>` |
| SQL Server + Dapper | **Yes** | Repositories under `Infrastructure/Persistence`; migrations via `DatabaseMigrationRegistry` |
| Traccar GPS | **Yes** | `TraccarSyncService`, `TraccarSyncOrchestrator`, `GpsSettings` / `SHEIKHGO_TRACCAR_*` |
| Device brands | **Yes** | `TrackerDtos` / `TrackerCatalogMigration`: Teltonika, Jimi, Queclink, Concox/GT06 |
| DeepInfra / MCP / AI | **Yes** | `DeepInfraAiProvider`, `AiChatGateway`, SheikhGo MCP (`user-sheikhgo`) |
| Vehicles / Drivers / Trips / Bookings | **Yes** | Controllers + Application features |
| GPS / Fuel / Maintenance / Alerts / Reports | **Yes** | See Part A |
| SignalR | **Yes** | e.g. WhatsApp hub; notifications path |
| Flutter driver / fleet apps | **Yes** | `Frontend/sheikh-driver`, `Frontend/sheikhGo-Fleet` (out of primary ERP scope but SOS/fuel/docs touch them) |

**Could not fully verify in this pass (no production DB queries, no live Traccar):**  
- Tenant-by-tenant enablement of module flags / premium alert add-ons  
- Real-world OBD fuel-level coverage %  
- Volume/quality of labeled accidents for risk calibration  
- Whether public tracking HTML in `wwwroot/track` is deployed in every environment  

---

# Part A — Existing-system audit

## A.0 Cross-cutting inventory

### Backend modules (MCP `list_modules` + filesystem)

Relevant healthy/partial modules include: `GpsTracking`, `FuelLogs`, `FuelFraud`, `Maintenance` / `MaintenanceModule`, `Assignments`, `Trips` (+ SmartAssignment), `SosDispatch`, `AccidentRisk`, `FleetEvAnomaly`, `FatigueDetection`, `SmartOps`, `Notifications`, `Reports` / `FleetReports`, `AI`, `Ocr`, `DriverApp`.

### Angular routes (ERP)

| Area | Module file | Key routes |
|------|-------------|------------|
| GPS | `Frontend/sheikhgo-erp/src/app/modules/gps-tracking/gps-tracking.module.ts` | `live`, `devices`, `alerts`, `sos`, `driver-performance`, `accident-risk`, `client-tracking-links`, … |
| Fleet | `…/fleet-management/fleet-management.module.ts` | `documents`, `ev`, `anomalies`, `maintenance/predictive`, assignments, … |
| Fuel | `…/fuel-logs/fuel-logs.module.ts` | ``, `fraud-events`, `efficiency`, `telemetry` |
| Trips | trips module | `smart-assignment` (menu: `/trips/smart-assignment`) |

GPS layout tabs already include **Driver Performance**, **Accident Risk**, **Client Links** (`gps-tracking-layout.component.ts`). SOS is routed (`sos`) and in fleet ops menu; **not** in the GPS top tab strip (UX gap).

### Jobs / hosted services (`DependencyInjection.cs`)

Notable: `TraccarSyncService`, `GpsOfflineDetectionHostedService`, `GpsDailyRollupHostedService`, `GpsAutoTripHostedService`, `PredictiveMaintenanceDriverNotifyHostedService`, `MaintenanceAlertHostedService`, `ComplianceReminderHostedService`, `FatigueEscalationHostedService`, `SmartOperationsHostedService`, `NotificationDispatchHostedService`, AI jobs/escalation.

### AuthZ patterns

- `[Authorize]` + `[RequirePermission(...)]` on controllers  
- Examples: `GpsPermissions.AlertView` (accident risk, SOS list), `FinancePermissions.FuelView/Update` (fuel fraud), `FleetPermissions.VehicleView/Update` (EV/docs/anomalies), `OperationsPermissions.TripView/Assign` (client links)  
- Public tracking: `[AllowAnonymous]` + rate limit `public` on `PublicTrackingController`

### Reports / AI

- Fleet reports queries; GPS analytics including driver scores  
- AI assistant tools include predictive maintenance helper usage (`AssistantPredictiveTools.cs`)  
- DeepInfra preferred provider for chat/MCP tool-calling — **not** used as the trip-assignment scorer today

---

## A.1 Driver Performance Scoring — **Partially implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| Calculator | `Backend/.../GpsTracking/Services/DriverScoreCalculator.cs` — overall + **Safety** + **FuelEfficiency** + **OnTime**; training/reward flags |
| Enrichment | `DriverPerformanceEnricher.cs` |
| Queries | `AnalyticsDriverQueries.cs` (`GetDriverScoreRankingQuery`), `DriverPerformanceProfileQueries.cs` |
| Schema | `DriverPerformanceMigration.cs` → `DriverViolations`, `DriverAttendance` |
| UI | `driver-performance-page.component.ts` → `/gps-tracking/driver-performance` |
| Nav | GPS layout + `menu-config.ts` analytics |

**Gaps / honesty**

- Explicitly **rule-based**; seatbelt **absent** (commented in calculator — no sensor).  
- Weights need **business sign-off** (code comment references Phase 10 open risk).  
- “Partial data” flag exists; coaching workflows / HR actions not a full product loop.  
- Website marketing page exists (`website/.../driver-performance`) — marketing ≠ ERP depth.

**Blocked?** No. Data quality depends on `GpsAlertEvents` harsh/overspeed attribution to `DriverId` and fuel logs / planned trip ends.

---

## A.2 Predictive Maintenance — **Partially implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| Rules | `PredictiveMaintenanceHelper.cs` — Oil / Tire / EngineLife intervals |
| Schema | `PredictiveMaintenanceMigration.cs` (vehicle oil/tire/engine columns) |
| API | `MaintenanceModuleController` / `MaintenanceController` dashboard includes predictive alerts (`MaintenanceModuleRepository.GetPredictiveAlertsAsync`) |
| Job | `PredictiveMaintenanceDriverNotifyHostedService` |
| UI | `/fleet/maintenance/predictive` |
| Tests | `PredictiveMaintenanceHelperTests.cs` |
| Broader maint | Work orders, workshops, schedules, parts pages under fleet-management |

**Gaps**

- **Not** predictive ML (no vibration/RUL models). Defaults when history missing.  
- OBD/engine DTC integration for true predictive remaining life **not verified** as production-complete.  
- Flutter maintenance hubs exist; ERP write depth for WO may still vary by role (see older `FLEET-MASTER-GAP-ROADMAP.md`).

**Blocked?** Soft-blocked on accurate odometer + service history hygiene.

---

## A.3 Shareable Tracking Links (no login) — **Already implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| Auth APIs | `GpsTrackingController.ClientLinks.cs` — list/create/revoke |
| Commands | `ClientTrackingLinkCommands.cs` |
| Public API | `PublicTrackingController` → `GET /api/public/tracking/{token}` `[AllowAnonymous]` + rate limit |
| Public page | `PublicTrackingPageController` → `GET /t/{token}` HTML |
| WhatsApp | `TrackingAndRatingCommands.cs` / automation path |
| UI | `client-tracking-links-page.component.ts` |

**Gaps**

- Map experience is minimal HTML (not full Google Maps SPA) unless `wwwroot/track` assets present.  
- Privacy: token secrecy, revoke UX, passenger PII minimization — needs product policy.  
- Branding / multi-language polish for customer-facing page.

**Blocked?** No for MVP packaging.

---

## A.4 AI Trip Assignment — **Hardened MVP** (Smart Trip Assignment)

**Exists (post Module 4 hardening)**

| Layer | Evidence |
|-------|----------|
| Policy | `SmartAssignmentPolicy` — SuggestOnly; create auto-assign off; EV buffer 15%; max hours 12 |
| Constraints | `TripAssignmentConstraints` — OnTrip, no vehicle, capacity, maintenance, shift, conflict, EV, type-if-required |
| Service | `TripSmartAssignmentService` — suggest (+ excluded) / confirm / override / auto (blocked) / shadow log |
| Scorer | `TripAssignmentScorer` — 9 factors incl. performance, cost, utilization (weights sum 1.0) |
| Schema | `TripSmartAssignmentDecisions` (+ Shadow / Confirmed outcomes) |
| API | `GET suggestions`, `POST confirm`, override, auto, decisions, recent |
| UI | `/trips/smart-assignment` — confirm CTA, excluded panel, Suggest only badge |
| Tests | `TripAssignmentScorerTests`, `TripAssignmentConstraintsTests` |

**Still deferred:** tenant AutoApply after shadow; configurable weights; LLM explain; trip `RequiredVehicleType` column; override analytics dashboard.

**Blocked?** Soft-blocked without live GPS + fuel/battery for best ranking quality.

---

## A.5 EV Fleet Support — **Partially implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| Schema | `FleetEvAnomalyMigration.cs` — `ElectricRangeKm`, `BatteryCapacityKwh`, `ChargingStations` |
| API | `FleetEvAnomalyController` — `GET api/fleet/ev/dashboard`, charging stations CRUD |
| Repo | `FleetEvAnomalyRepository.cs` |
| UI | `/fleet/ev` `ev-dashboard-page.component.ts` |
| Seed/nav | `ElectricFleetDemoSeedMigration`, `ElectricFleetNavMenuMigration` |
| Assignment | Range-aware skip logic referenced in UI + scorer |

**Gaps**

- Live SOC depends on device telemetry mapping (`GpsObdTelemetryMapper` / ingest) — coverage unknown.  
- No full charging-session billing / OCPI network integration found in this audit.  
- FuelType enum in fleet domain catalog still Petrol/Diesel/CNG — EV may be flagged via other vehicle fields (verify on implement).

**Blocked?** Hardware/telemetry for battery %; many Gulf fleets still ICE-first (**demand validation** needed).

---

## A.6 Document Tracker — **Partially implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| API | `GET/POST api/fleet/documents/tracker` on `FleetEvAnomalyController` |
| Schema extras | Registration/Fitness expiry columns on Vehicles; `VehicleDocumentOcrMigration` |
| Job | `ComplianceReminderHostedService` |
| UI | `/fleet/documents` `documents-tracker-page.component.ts` (Insurance/Registration/Fitness/Permit/Other) |
| Driver app | `DriverDocumentsCommands` (driver-side docs path) |
| OCR | `OcrController` |

**Gaps**

- Unified driver+vehicle+company document vault, e-sign, insurance broker APIs not verified.  
- OCR accuracy / storage retention policy needs product rules.  
- Duplicate “Compliance” vs “Documents” menu entries in `menu-config.ts`.

**Blocked?** No for expiry tracker MVP.

---

## A.7 Anomaly Detection — **Partially implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| Evaluator | `FleetAnomalyEvaluator.cs` on GPS ingest — `night_unknown_area`, sudden speed drop, off-route vs trip pickup/destination |
| Table | `FleetAnomalyEvents` (`FleetEvAnomalyMigration`) |
| API | `GET api/fleet/anomalies`, review POST |
| UI | `/fleet/anomalies` |
| Related | GPS alerts (geofence, overspeed, sos, offline), Fatigue module, SmartOps scanners |

**Gaps**

- Rule-based only; no unsupervised ML.  
- False-positive rate unquantified; investigation workflow is basic review notes.  
- SOS / harsh / geofence live in **GpsAlertEvents** — product should clarify “anomaly” vs “alert” taxonomy.

**Blocked?** No; quality blocked on GPS sample rate and trip status accuracy.

---

## A.8 Fuel Management — **Already implemented** (core ops)

**Exists**

| Layer | Evidence |
|-------|----------|
| API | `FuelLogsController` CRUD + efficiency + telemetry |
| App | Create/Update/Delete + `FuelModuleQueries` |
| Infra | `FuelLogRepository`, `FuelObdTelemetryMigration` |
| UI | `/fuel-logs`, efficiency, telemetry |
| Driver | `DriverFuelCommands`, receipt migration |
| Domain | `FuelLogs` table in fleet domain catalog |

**Gaps**

- Fleet fuel cards, station contracts, multi-currency tax packs — not fully audited as complete.  
- CNG/Petrol/Diesel catalog; EV energy as “fuel” is separate (EV module).

**Blocked?** No for log + efficiency MVP.

---

## A.9 Fuel Fraud Detection — **Partially implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| Schema | `FuelPumpOrders` (`FuelFraudDetectionMigration`) + module `ALERT_FUEL_FRAUD` |
| Verify | `FuelFraudVerifier.cs` + `FuelFraudVerification.cs` — ordered liters vs OBD % increase |
| API | `FuelFraudController` events + resolve + platform list |
| Driver submit | `DriverSubmitFuelPumpOrderCommand` |
| UI | `/fuel-logs/fraud-events`, platform admin page |
| Tests | `FuelFraudVerificationTests.cs` |
| Settings | `GpsSettings.FuelFraud` enable/tolerances |

**Gaps / blockers**

- **Hard dependency:** devices reporting trustworthy `FuelLevel` during/after fill. Without OBD/CAN fuel %, feature is **Blocked** for that vehicle.  
- False positives: tank capacity wrong, sloping ground, delayed telemetry, multi-fill. Investigation UX is resolve-only.  
- Not ML fraud scoring.

---

## A.10 SOS Auto-Dispatch — **Partially implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| Plan builder | `SosAutoDispatchService.cs` — AutoAssigned / Suggested / DispatchOnly via Haversine nearest verified team |
| Schema | `SosAutoDispatchMigration` enriches `DriverSosAlerts`; `SosRecoveryPartners` |
| API | `SosDispatchController` events/partners/resolve |
| Trigger | Driver app SOS commands call auto-dispatch (`DriverAppCommands`) |
| UI | `/gps-tracking/sos` |
| Tests | `GeoDistanceTests.cs` |
| Related | `GmeetOfflineSosSmsMigration`, fatigue/SOS alert types in GPS domain |

**Gaps**

- No verified automatic call to third-party towing APIs / emergency services.  
- Partners are contact-card records (seeded PK/AE examples).  
- GPS nav tab missing for SOS; operator may miss it.  
- “AutoAssigned” needs verified team pool + live locations.

**Blocked?** Soft-blocked without staffed recovery partners and verified team GPS.

---

## A.11 Predictive Accident Risk Score — **Partially implemented**

**Exists**

| Layer | Evidence |
|-------|----------|
| Calculator | `AccidentRiskCalculator.cs` — driver & route factors (accidents, speed, fatigue, distance) |
| API | `GET api/gps/accident-risk/dashboard` |
| Repo | `AccidentRiskRepository.cs` |
| Proactive | `AccidentRiskProactiveScanner.cs` |
| UI | `/gps-tracking/accident-risk` |
| Tests | `AccidentRiskCalculatorTests.cs` |

**Gaps**

- Rule-based risk, **not** actuarial ML.  
- Quality depends on logged accidents + fatigue camera events + overspeed alerts.  
- No proven correlation study in-repo.  
- Should not auto-suspend drivers without HR policy.

**Blocked?** Soft-blocked on sparse accident labels (common for new tenants).

---

## A.12 Classification matrix (quick)

| Module | Class | Primary blocker |
|--------|-------|-----------------|
| 1 Driver score | Partial (strong) | Weight/policy sign-off; telematics attribution |
| 2 Pred. maint | Partial | History hygiene; do not market as ML |
| 3 Share links | **Already** | Privacy/brand polish |
| 4 Trip assign | Partial | Telemetry + Routes; naming honesty |
| 5 EV | Partial | Battery telemetry + customer EV demand |
| 6 Documents | Partial | OCR/storage/policy |
| 7 Anomalies | Partial | FP tuning; taxonomy vs alerts |
| 8 Fuel mgmt | **Already** (core) | Advanced commercial fuel ops |
| 9 Fuel fraud | Partial | **OBD fuel level hardware** |
| 10 SOS | Partial | Partners + verified responders |
| 11 Accident risk | Partial | Labeled incidents; HR policy |

---

# Part B — Business plan (per module)

*Estimates labeled as **assumptions**. No invented revenue guarantees.*

### Shared commercial framing

- **Customer:** Mid-size Gulf / Pakistan / UAE transport, Umrah/Hajj fleets, corporate shuttle, rental/transfer operators using SheikhGo ERP + GPS.  
- **Pricing approach (assumption):** Package as **Fleet Intelligence** add-ons on subscription (per vehicle/month or module bundle), not one-off custom.  
- **Demand validation:** Interview 3–5 operators; measure which screens they open weekly; hardware attach rate for OBD.  
- **Privacy / risks:** Driver monitoring laws, customer location sharing consent, false-accusation risk (fraud/risk scores), insurance claims misuse.

---

### 1. Driver Performance Scoring

| Item | Content |
|------|---------|
| Problem | Ops cannot fairly rank drivers; safety coaching is ad-hoc |
| Value | Reduce incidents; reward top 20%; training flags |
| MVP | Ranking table Safety/Fuel/On-time; date range; export CSV |
| Advanced | Coaching plans, payroll bonus hooks, WhatsApp weekly digest |
| Deps | GPS alerts, fuel logs, completed trips with plans |
| HW/3P | Harsh events need capable trackers (Teltonika preferred) |
| Data | Available if devices online + DriverId on alerts |
| Pricing | Include in GPS Analytics tier (**assumption**) |
| Risks | Driver trust; gaming; incomplete data “Poor” labels |
| KPIs | % drivers scored; coaching completion; overspeed ↓ |
| Validation | Ask: “Would you dock pay on this score?” If no → coaching-only |

### 2. Predictive Maintenance

| Item | Content |
|------|---------|
| Problem | Breakdowns on trip; missed oil/tire intervals |
| Value | Downtime ↓, workshop scheduling |
| MVP | Oil/tire/engine due list + notify driver/manager |
| Advanced | Parts inventory link, vendor SLA, DTC-based RUL |
| Deps | Mileage accuracy, WO history |
| HW/3P | Optional OBD; workshops |
| Data | Partial today |
| Pricing | Maintenance module SKU |
| Risks | Over-alerting; liability if “predicted” failure missed |
| KPIs | Overdue jobs ↓; unplanned WO ↓ |
| Validation | Compare last 90 days breakdowns vs rule alerts |

### 3. Shareable Tracking Links

| Item | Content |
|------|---------|
| Problem | Customers ask “where is my vehicle?” on WhatsApp |
| Value | CS load ↓; premium transfer experience |
| MVP | Create/revoke link; public live ETA page |
| Advanced | Branded map, multi-stop, chat-to-ops |
| Deps | Live GPS; trip linkage |
| HW/3P | None beyond GPS |
| Data | Ready |
| Pricing | Ops add-on or included in GPS |
| Risks | Token leak; stalking; GDPR-like expectations |
| KPIs | Links created; support tickets ↓ |
| Validation | Highest quick-win — watch link creation rate |

### 4. AI Trip Assignment

| Item | Content |
|------|---------|
| Problem | Dispatchers waste time; biased assignment |
| Value | Faster dispatch; fatigue/fuel-aware |
| MVP | Ranked suggestions + override reason log |
| Advanced | Auto-assign SLA, learning from overrides (**later**, needs data) |
| Deps | Live positions, ratings, fatigue hours, Routes API |
| HW/3P | Google Routes; GPS |
| Data | Medium |
| Pricing | Dispatch / Smart Ops tier |
| Risks | Over-trust; fairness complaints |
| KPIs | Time-to-assign; override rate; on-time pickup |
| Validation | Shadow mode 2 weeks before auto-assign |

### 5. EV Fleet Support

| Item | Content |
|------|---------|
| Problem | Range anxiety kills EV adoption for fleets |
| Value | Dispatch only when range OK; charger map |
| MVP | Battery board + range alerts + charging POIs |
| Advanced | Charger booking, energy cost/km, depot charging schedules |
| Deps | SOC telemetry |
| HW/3P | EV CAN/OBD or OEM API |
| Data | Weak unless EV fleet exists |
| Pricing | Niche add-on (**assumption**: lower near-term demand vs ICE fuel fraud) |
| Risks | Building for demo tenants only |
| KPIs | Range-related trip failures ↓ |
| Validation | Confirm % EV in pipeline customers |

### 6. Document Tracker

| Item | Content |
|------|---------|
| Problem | Expired insurance/registration grounds vehicles |
| Value | Compliance; avoid fines |
| MVP | Expiry list + reminders |
| Advanced | OCR ingest, broker portal, e-archive |
| Deps | Notifications |
| HW/3P | Optional OCR (Tesseract already in ERP deps) |
| Data | Ready for manual entry |
| Pricing | Compliance bundle |
| Risks | Wrong expiry → false confidence |
| KPIs | Docs expired while active = 0 |
| Validation | Ops already ask for this — strong |

### 7. Anomaly Detection

| Item | Content |
|------|---------|
| Problem | Theft / hijack / route deviation missed until too late |
| Value | Earlier ops intervention |
| MVP | Night unknown area + off-route + speed drop + review queue |
| Advanced | Pattern learning per route; camera fusion |
| Deps | Trips active status; GPS density |
| HW/3P | Higher-rate trackers |
| Data | Generating events if enabled |
| Pricing | Safety / Alerts premium |
| Risks | Alert fatigue; false accusations |
| KPIs | MTTA (mean time to acknowledge); FP rate |
| Validation | Measure FP before selling “AI anomaly” |

### 8. Fuel Management

| Item | Content |
|------|---------|
| Problem | Fuel is #1 variable cost |
| Value | Cost/km visibility |
| MVP | Logs + efficiency report + telemetry view |
| Advanced | Cards, station network, budget caps |
| Deps | Driver app receipts helpful |
| HW/3P | Optional OBD |
| Data | Available |
| Pricing | Core fleet or Finance |
| Risks | Incomplete logs |
| KPIs | SAR/km; fill compliance |
| Validation | Universal demand |

### 9. Fuel Fraud Detection

| Item | Content |
|------|---------|
| Problem | Pump theft / short-fill |
| Value | Direct cost recovery |
| MVP | Order liters → verify OBD rise → alert → resolve |
| Advanced | Station geofence, camera, card matching |
| Deps | **Fuel level sensors** |
| HW/3P | Teltonika/FMC with fuel; tank calibration |
| Data | Only on instrumented vehicles |
| Pricing | Premium per vehicle with fuel sensor (**assumption**) |
| Risks | Accusing honest drivers; calibration |
| KPIs | Alerts investigated; confirmed fraud liters |
| Validation | Pilot 10 calibrated tanks before wide sell |

### 10. SOS Auto-Dispatch

| Item | Content |
|------|---------|
| Problem | Driver SOS with no structured response |
| Value | Safety duty of care; brand trust |
| MVP | Alert ops + nearest team + partner phone |
| Advanced | Auto-SMS partners; voice bridge; insurance notify |
| Deps | Verified team GPS; partner directory |
| HW/3P | SMS gateway; optional call center |
| Data | Partial |
| Pricing | Safety pack |
| Risks | Liability if “auto-dispatch” fails; medical emergencies |
| KPIs | SOS→ack time; false SOS rate |
| Validation | Run tabletop drill with ops |

### 11. Predictive Accident Risk

| Item | Content |
|------|---------|
| Problem | Repeat risky drivers/routes unnoticed |
| Value | Proactive assignment avoidance / coaching |
| MVP | Dashboard High/Moderate/Low + factors |
| Advanced | Insurer reports; ML with labeled claims |
| Deps | Accident logging culture; fatigue hardware optional |
| HW/3P | Optional DMS/fatigue cam |
| Data | Often sparse |
| Pricing | Safety analytics |
| Risks | Discrimination claims; bad labels |
| KPIs | High-risk drivers coached; incident rate |
| Validation | Only sell after 6+ months labeled data or keep as “indicator” |

---

# Part C — Technical architecture

## C.1 Principles

1. **Modular monolith** — new capability = Application feature folder + Dapper repo + controller + Angular module page.  
2. **Same DB** (tenant-scoped SQL Server). No new OLTP database.  
3. **No broker / microservices** unless a future event volume/SLA forces outbox+worker split (not justified now; HostedServices already cover scanners).  
4. **No ML platform** for v1–v2: keep pure C# calculators + SQL aggregates. DeepInfra/MCP for **assistant explanation / ops Q&A**, not core scoring.  
5. **Ingest path** remains GPS position ingest → evaluators (`FuelFraudVerifier`, `FleetAnomalyEvaluator`, geofence/speed) → `GpsAlertEvents` / domain tables → `NotificationDecisionEngine`.  
6. **Traccar** remains device ingress/sync; SheikhGo owns business intelligence.  
7. **AuthZ** via existing permission codes; audit via existing audit/notification patterns.  
8. **Feature flags / Modules table** already used (e.g. `ALERT_FUEL_FRAUD`) — continue for packaging.

## C.2 Logical view

```text
[Devices: Teltonika/Jimi/Queclink] → Traccar → Sync/Ingest
        → VehicleCurrentLocation / GpsPositions / GpsAlertEvents
        → Evaluators (fraud, anomaly, geofence, offline)
        → Notifications / SignalR / WhatsApp

[ERP Angular] ← JWT + permissions ← API Controllers ← MediatR Features
[Driver Flutter] → SOS / Fuel pump order / Docs
[Public /t/{token}] → AllowAnonymous tracking API

[DeepInfra + MCP] → Assistant tools (read fleet/GPS/maint) — advisory
```

## C.3 Where each module lives

| Module | Backend home | Frontend home |
|--------|--------------|---------------|
| Driver score | `Features/GpsTracking` | `gps-tracking/driver-performance` |
| Pred. maint | `Features/MaintenanceModule` | `fleet-management/maintenance/predictive` |
| Share links | `GpsTracking` + `WhatsApp` public | `client-tracking-links` + `/t` |
| Trip assign | `Features/Trips/SmartAssignment` | `trips/smart-assignment` |
| EV + Anomaly + Docs | `Features/Fleet/EvAnomaly` + repos | `fleet/ev`, `anomalies`, `documents` |
| Fuel | `Features/FuelLogs` | `fuel-logs` |
| Fuel fraud | `Features/FuelFraud` + GPS verifier | `fuel-logs/fraud-events` |
| SOS | `Features/SosDispatch` + DriverApp | `gps-tracking/sos` |
| Accident risk | `Features/AccidentRisk` | `gps-tracking/accident-risk` |

## C.4 Justified exceptions (only if later)

| Exception | When justified |
|-----------|----------------|
| Separate analytics DB / warehouse | Multi-year GPS history query pain |
| ML service | Labeled fraud/accident datasets + proven lift over rules |
| Message bus | Multi-region write fan-out beyond one API host |

---

# Part D — Scoring / predictive design

## D.1 Separate metrics (already sketched in code)

| Metric | Definition (current) | Inputs | Type |
|--------|----------------------|--------|------|
| **Safety** | `ComputeSafetyScore` | Overspeed + harsh event counts | **Rule-based** |
| **Fuel** | `ComputeFuelEfficiencyScore` | Distance km / fuel liters (neutral 70 if missing) | **Rule-based** |
| **Punctuality** | `ComputeOnTimeScore` | Completed trips with plan vs on-time | **Rule-based** |
| Overall | Penalty buckets + idle/night | Alerts + GpsTrips + optional Traccar idle | **Rule-based** |

**Statistical (phase 2 option):** z-score drivers vs fleet peer group for overspeed rate per 100 km (still not ML).  
**ML (phase 3+, optional):** only with labeled outcomes; keep explainability.

## D.2 Trip assignment transparency

`TripAssignmentScorer` already returns **named factors with weights and details**. Product requirement: UI must show the factor list (not a black-box “AI score”). Auto-assign must log `TripSmartAssignmentDecisions` with outcome Proposed/Overridden.

## D.3 Fuel fraud & anomaly investigation / false positives

**Required investigation workflow (product):**

1. Alert created → status Pending.  
2. Ops opens event: map, pump order, OBD before/after, tank capacity, speed, station.  
3. Actions: Confirm fraud / False positive / Needs calibration / Escalate.  
4. FP reasons feed threshold tuning (`GpsSettings.FuelFraud` tolerances).  
5. Never auto-deduct driver wallet without human confirm (**business rule — Ali decision**).

**Anomaly FP controls:** cooldown windows, severity, require active trip for off-route, known-area radius (already 3 km / 30-day heuristic in evaluator).

## D.4 Accident risk

Keep as **risk indicator** with factor cards (`AccidentRiskCalculator`). High-risk threshold 65. Proactive messages exist — gate auto-blocking of assignment behind policy flag.

## D.5 Predictive maintenance

Keep rule engine; document defaults. Future “predictive” marketing copy must say **“due-date prediction from intervals/odometer”** until ML exists.

---

# Part E — Module specs (planning; no code)

*Each module: FR / stories / schema / APIs / UI / jobs / integrations / permissions / tests / deploy.*

### E.1 Driver Performance

**FR:** Rank drivers by overall + Safety/Fuel/On-time for date range; flag training/reward; show partial-data.  
**Stories:** As fleet manager, I filter last 30 days and export ranking so I can run weekly coaching.  
**AC:** Scores match calculator unit tests; missing fuel → fuel subscore 70 not 0; permission gated.  
**Schema:** Existing `DriverViolations` / `DriverAttendance`; optional later `DriverScoreSnapshots`.  
**APIs:** Existing ranking query; optional snapshot POST.  
**Angular:** Harden current page (export, drill-down to factors).  
**Jobs:** Optional weekly digest via notifications.  
**Permissions:** Align with GPS analytics view.  
**Tests:** Calculator + enricher unit tests; API auth test.  
**Deploy/rollback:** App-only; feature flag hide menu.

### E.2 Predictive Maintenance

**FR:** List oil/tire/engine alerts with severity; notify.  
**AC:** Overdue when km/days past interval; Critical/Warning bands match helper.  
**Schema:** Existing vehicle predictive columns.  
**APIs:** Dashboard predictiveAlerts.  
**UI:** Predictive page + link to create WO.  
**Jobs:** `PredictiveMaintenanceDriverNotifyHostedService` (tune cadence).  
**Tests:** Helper tests already — add integration on dashboard.  
**Rollback:** Disable notify hosted service via config.

### E.3 Shareable Tracking Links

**FR:** Create time-boxed token for trip; public poll position; revoke.  
**AC:** Expired/revoked → 404 message; rate limited; no auth cookie required.  
**Schema:** Existing client tracking link table (via commands/migration history).  
**APIs:** Client links + `/api/public/tracking/{token}` + `/t/{token}`.  
**UI:** ERP manager + public page polish.  
**Integrations:** WhatsApp send link.  
**Permissions:** TripAssign to create; public read.  
**Tests:** Token expiry; tenant isolation.  
**Rollback:** Revoke all active links script (ops).

### E.4 AI Trip Assignment (Smart Trip Assignment)

**FR:** Hard-filter then score; suggest + explain; staff confirm; override with reason; no create-time auto.  
**AC:** Weights sum to 1.0; excluded reasons returned; confirm writes audit; EV 15% buffer; SuggestOnly default.  
**Schema:** Smart assignment decisions (Confirmed / Shadow / …).  
**APIs:** suggestions, confirm, override, auto (gated), decisions.  
**UI:** `/trips/smart-assignment`.  
**Integrations:** Google Routes; driver performance history; fuel logs util.  
**Permissions:** TripView / TripAssign.  
**Tests:** Scorer + constraint matrix.  
**Rollback:** Keep confirm/suggest; AutoApply stays off.

### E.5 EV Fleet

**FR:** Dashboard battery/range; CRUD charging stations; assignment awareness.  
**AC:** Range alert when estimated range &lt; threshold; stations tenant-scoped.  
**Schema:** Existing EV columns + ChargingStations.  
**APIs:** `api/fleet/ev/*`.  
**UI:** EV page.  
**Integrations:** Telemetry SOC.  
**Permissions:** VehicleView/Update.  
**Tests:** Dashboard empty-state; assignment skip unit.  
**Rollback:** Hide menu module.

### E.6 Document Tracker

**FR:** Upsert vehicle docs with expiry; list renewal status; reminders.  
**AC:** Expired highlighted; reminder job sends once per window.  
**Schema:** Docs store + vehicle expiry columns; OCR tables if used.  
**APIs:** documents/tracker.  
**UI:** documents page.  
**Jobs:** ComplianceReminderHostedService.  
**Permissions:** VehicleUpdate to write.  
**Tests:** Reminder idempotency.  
**Rollback:** Disable reminder service.

### E.7 Anomaly Detection

**FR:** Detect night/off-route/speed-drop; list/review.  
**AC:** Review marks IsReviewed; notifications respect policy.  
**Schema:** FleetAnomalyEvents.  
**APIs:** anomalies list/review.  
**UI:** anomaly-review page.  
**Integrations:** Ingest pipeline.  
**Permissions:** VehicleView/Update.  
**Tests:** Evaluator unit with synthetic positions.  
**Rollback:** Config kill-switch on evaluator.

### E.8 Fuel Management

**FR:** CRUD fuel logs; efficiency; telemetry.  
**AC:** Tenant isolation; validation on liters/odometer.  
**Schema:** FuelLogs (+ OBD fields).  
**APIs:** FuelLogsController.  
**UI:** fuel-logs module.  
**Permissions:** Finance Fuel*.  
**Tests:** CRUD authz.  
**Rollback:** N/A (core).

### E.9 Fuel Fraud

**FR:** Driver pump order → verify on leave-station/OBD → alert → resolve.  
**AC:** Shortfall beyond tolerance → fraud status + GpsAlertEvent; disable when FuelFraud.Enabled false.  
**Schema:** FuelPumpOrders.  
**APIs:** fuel-fraud + driver submit.  
**UI:** fraud-events (+ platform).  
**Integrations:** Ingest FuelLevel.  
**Permissions:** FuelView/Update; platform tenants view.  
**Tests:** Verification matrix.  
**Rollback:** `GpsSettings.FuelFraud.Enabled=false`.

### E.10 SOS Auto-Dispatch

**FR:** On SOS, build plan; assign nearby verified team if within max km; else suggest + partner.  
**AC:** Modes AutoAssigned/Suggested/DispatchOnly; confirmation message to driver.  
**Schema:** DriverSosAlerts enrichments; SosRecoveryPartners.  
**APIs:** sos-dispatch; driver SOS.  
**UI:** sos page + add GPS nav tab.  
**Integrations:** SMS optional.  
**Permissions:** AlertView/Resolve.  
**Tests:** GeoDistance; plan modes.  
**Rollback:** Force DispatchOnly via config.

### E.11 Accident Risk

**FR:** Dashboard drivers/routes with factors and bands; optional proactive notify.  
**AC:** Score clamp 0–100; High ≥ 65; no silent auto-suspend.  
**Schema:** Uses existing accident/fatigue/alert sources (repo).  
**APIs:** accident-risk/dashboard.  
**UI:** accident-risk page.  
**Jobs:** Proactive scanner cadence.  
**Permissions:** Gps.AlertView.  
**Tests:** Calculator tests.  
**Rollback:** Disable scanner.

---

# Part F — Roadmap

## F.1 Priority principles

Align with go-live skill: **Web/ERP + backend first**; don’t block production on ML. Prefer **harden existing** over new greenfield modules.

## F.2 Phases

| Phase | Theme | Modules | Dev-days (assumption) | Skills |
|-------|-------|---------|----------------------|--------|
| **0** | Package & honesty | Docs/marketing copy; rename “AI” where heuristic; menu cleanup | 3–5 | PM + FE |
| **1** Quick wins | Share links polish; Driver score UX/export; Documents reminders QA; Fuel CRUD polish | 3,1,6,8 | 15–25 | Full-stack |
| **2** Safety ops | SOS nav+drill; Anomaly FP cooldowns; Accident risk policy gates; Alerts taxonomy | 10,7,11 | 20–30 | Full-stack + GPS |
| **3** Dispatch intelligence | Smart assignment shadow→auto; Routes reliability; EV range gate | 4,5 | 15–25 | Full-stack + Maps |
| **4** Fuel intelligence | Fraud pilot on calibrated fleet; investigation UX; tank calibration tools | 9 (+8) | 20–35 | GPS/telematics |
| **5** Maint maturity | Pred. maint → WO one-click; history hygiene; optional DTC | 2 | 15–25 | Full-stack |
| **6** Optional ML | Only if labeled data + lift proven | 7,9,11 | 40–80 | Data/ML eng |

*Developer-days are planning assumptions for 1 mid/senior full-stack familiar with this repo; parallelization can compress calendar time.*

## F.3 Dependency matrix

```text
Share links ─────────────► Live GPS, Trips
Driver score ────────────► GpsAlertEvents, FuelLogs, Trips
Pred. maint ─────────────► Vehicles mileage, Maintenance history
Trip assign ─────────────► Live GPS, Ratings, Fatigue hours, Routes API
EV ──────────────────────► SOC telemetry, Vehicles EV fields
Documents ───────────────► Notifications
Anomaly ─────────────────► Ingest, Active trips
Fuel mgmt ───────────────► (core)
Fuel fraud ──────────────► Fuel mgmt + OBD FuelLevel + tank capacity
SOS ─────────────────────► Driver app, verified team GPS, partners
Accident risk ───────────► Accidents log, speed alerts, fatigue (optional)
```

## F.4 Quick wins vs data/ML-heavy

| Quick wins | Data / hardware / ML-heavy |
|------------|----------------------------|
| Share links polish | Fuel fraud (OBD) |
| Driver score export + weight config UI | Accident risk ML |
| Document reminders QA | Unsupervised anomaly ML |
| SOS GPS tab + partner CRUD UX | EV without SOC devices |
| Smart assignment factor panel | Auto-assign without shadow metrics |

## F.5 Recommended first 3 — rationale

1. **Shareable Tracking Links** — Already implemented; high customer-visible value; low hardware risk; differentiates transfers/Umrah ops.  
2. **Driver Performance Scoring** — Strong MVP; Safety/Fuel/Punctuality already separated; sells “fleet intelligence” honestly as rules; feeds assignment later.  
3. **Fuel Management (+ Fraud as gated add-on)** — Fuel is always-on pain; fraud only enabled where sensors exist (avoid false product claims).

*Alternative if safety-first buyer:* swap #3 for **SOS Auto-Dispatch** packaging.

## F.6 Definition of Done (suite phase)

- Audit class upgraded or documented remaining gaps  
- Permissions + tenant isolation tests green  
- Menu IA consistent (no duplicate dead links)  
- Marketing copy matches algorithm class (rule vs ML)  
- Runbook: enable/disable flags, FP investigation  
- No secrets in repo; MSP pre-push clean when implementing

## F.7 Risks (roadmap-level)

| Risk | Mitigation |
|------|------------|
| Selling “AI/ML” for rule engines | Copy + UI labels |
| Alert fatigue | Cooldowns, severity, module packaging |
| Hardware variance (Jimi vs Teltonika) | Tracker capability matrix in catalog |
| Scope creep to microservices | This document’s architecture stance |
| Driver trust / HR misuse | Coaching-first defaults; Ali policy decisions |

---

# Part G — Final deliverables checklist

- [x] Exec summary  
- [x] Existing-system audit (Part A) with paths/classes  
- [x] Business plan per module (Part B)  
- [x] Technical architecture (Part C)  
- [x] Scoring/predictive design (Part D)  
- [x] Module specs outline (Part E)  
- [x] Dependency matrix + roadmap (Part F)  
- [x] Risks / privacy notes  
- [x] Questions for Ali (below)  
- [x] Implementation-prompt outline per module (below)  

**Also referenced:** `docs/FLEET-MASTER-GAP-ROADMAP.md` (Flutter role shells — complementary, Jul 2026).

---

## Questions requiring Ali’s business decisions

1. **Positioning:** Market Smart Assignment / scores as **“AI-assisted heuristics”** or rename to avoid AI washing?  
2. **Driver scores & HR:** Coaching-only vs pay impact? Who can see rankings (drivers themselves)?  
3. **Auto-assign:** Allow unattended auto-assign, or suggestions-only until override rate &lt; X%?  
4. **Fuel fraud:** Auto wallet deduction after confirm? Who adjudicates FP?  
5. **SOS liability:** What is promised SLA to customers when “auto-dispatch” runs?  
6. **EV investment:** What % of 2026–27 pipeline is EV? Pause EV polish if &lt; threshold?  
7. **Accident risk:** Can high risk **block** assignment, or warn only?  
8. **Pricing packs:** Bundle all 11 vs Safety / Fuel / Dispatch / Compliance SKUs?  
9. **Score weights:** Approve `DriverScoreCalculator` / `TripAssignmentScorer` weights as official?  
10. **Public tracking privacy:** Max link TTL? Show driver name/phone to customer?  
11. **Hardware attach:** Mandate Teltonika+fuel sensor for fraud SKU?  
12. **Go-live priority:** Keep P1 Web/ERP reliability ahead of Intelligence polish?

---

## Implementation-prompt outlines (one per module)

*Paste into a future Agent session when ready to build. Each assumes: modular monolith, net10, no new DB/broker, follow existing CQRS/Angular patterns, tests, permissions, MSP pre-push before push.*

### Prompt — 1 Driver Performance
Harden ERP driver-performance: factor drill-down, CSV export, tenant-configurable weights (stored settings), document Safety/Fuel/On-time. Extend tests for enricher. Do not add seatbelt. No ML.

### Prompt — 2 Predictive Maintenance
Wire predictive alert → create maintenance request/WO; surface defaults in UI; verify notify hosted service idempotency; update copy to “interval-based prediction.”

### Prompt — 3 Shareable Tracking Links
Polish `/t/{token}` UX (map if Maps key policy allows), branding, TTL presets, WhatsApp template; security review token entropy/revoke; FE create-from-trip detail.

### Prompt — 4 AI Trip Assignment
Shadow-mode metrics dashboard; show factor panel; tenant flag AutoAssign; EV range buffer configurable; log all overrides; keep TripAssignmentScorer as source of truth (optional DeepInfra “explain these factors” only).

### Prompt — 5 EV Fleet
SOC quality indicators; charging station map; assignment integration tests; hide module when zero EVs; no OCPI unless decided.

### Prompt — 6 Document Tracker
Unify compliance+documents nav; reminder digests; OCR upload path hardening; expiry KPIs on fleet dashboard.

### Prompt — 7 Anomaly Detection
Cooldownonomy vs GpsAlertEvents; cooldown/config; investigation statuses; FP analytics; optional geofence correlation.

### Prompt — 8 Fuel Management
Efficiency report UX; telemetry panel QA; driver receipt linkage; cost/km KPI widget.

### Prompt — 9 Fuel Fraud
Investigation workspace (map+OBD+order); calibration checklist; enablement only if FuelLevel present; platform ops view; never auto-punish.

### Prompt — 10 SOS Auto-Dispatch
Add SOS to GPS nav; partner CRUD UX; tabletop runbook; SMS hook behind flag; DispatchOnly fallback config; verify team eligibility rules.

### Prompt — 11 Accident Risk
Policy flag block-vs-warn; factor UI polish; proactive scanner schedule; require minimum sample size before High band actions; keep rule calculator.

---

## Appendix — Key absolute paths (quick index)

- Calculators:  
  `/Users/alirazatahir/Projects/Sheikh-Travel-System/Backend/SheikhTravelSystem.Application/Features/GpsTracking/Services/DriverScoreCalculator.cs`  
  `/Users/alirazatahir/Projects/Sheikh-Travel-System/Backend/SheikhTravelSystem.Application/Features/Trips/SmartAssignment/TripAssignmentScorer.cs`  
  `/Users/alirazatahir/Projects/Sheikh-Travel-System/Backend/SheikhTravelSystem.Application/Features/AccidentRisk/Services/AccidentRiskCalculator.cs`  
  `/Users/alirazatahir/Projects/Sheikh-Travel-System/Backend/SheikhTravelSystem.Application/Features/MaintenanceModule/PredictiveMaintenanceHelper.cs`
- Evaluators:  
  `.../Infrastructure/Services/Gps/FuelFraudVerifier.cs`  
  `.../Infrastructure/Services/Gps/FleetAnomalyEvaluator.cs`  
  `.../Infrastructure/Services/Sos/SosAutoDispatchService.cs`
- Controllers: `FuelFraudController`, `SosDispatchController`, `AccidentRiskController`, `FleetEvAnomalyController`, `GpsTrackingController.ClientLinks.cs`, `FuelLogsController`, `TripsController`
- Angular: `gps-tracking.module.ts`, `fleet-management.module.ts`, `fuel-logs.module.ts`, `menu-config.ts`, `gps-tracking-layout.component.ts`
- Migrations registry: `DatabaseMigrationRegistry.cs`

---

*End of plan. Stop here — no implementation in this pass.*
