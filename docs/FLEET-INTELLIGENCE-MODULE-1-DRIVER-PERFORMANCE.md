# Fleet Intelligence — Module 1: Driver Performance Scoring

**Status:** Implemented (extend existing GPS stack)  
**Stack:** Angular ERP + ASP.NET Core (`net10.0`) + SQL Server + Traccar/GPS  
**Canonical surfaces:** `DriverScoreCalculator`, `DriverPerformanceEnricher`, `GET api/gps/analytics/drivers/ranking`, `/gps-tracking/driver-performance`

## Business objective

Fair, transparent, rule-based evaluation of drivers on **Safety**, **Fuel Efficiency**, and **Delivery Punctuality**. No ML and no AI-invented inputs.

## What already existed (verified)

| Capability | Location |
|---|---|
| Fleet ranking + date filters | `GET …/analytics/drivers/ranking`, ERP page |
| Safety from overspeed + harsh | `GpsAlertEvents` (+ Traccar harsh fallback) |
| Fuel liters + GPS distance | `FuelLogs` + attributed `GpsTrips` |
| On-time vs planned end | `Trips.ActualEnd` / `PlannedEnd` (+ 15 min grace) |
| Assignment attribution | `AssignmentHistory` time-window join |
| `IsPartial` when Traccar gap | Ranking DTO flag |
| Driver app self-score | `GET …/driver-app/performance/score` |
| Permission | `Driver.ViewPerformance` |

**Parallel (not Module 1 scoring):** Drivers drawer “Performance” = bookings/revenue/violations/attendance + manual rating.

## Formulas (rule-based)

### Defaults (relative weights — need not sum to 100)

| Dimension | Default weight |
|---|---|
| Safety | **40** |
| Fuel | **30** |
| On-time | **30** |

Tenant admins with `Driver.ManagePerformanceWeights` can change weights (audited command `UpdateDriverPerformanceWeights`).

### Canonical Overall

```
Overall = round( (Safety×Wₛ + Fuel×Wբ + OnTime×Wₒ) / (Wₛ+Wբ+Wₒ) )
clamp to 0–100
```

If all weights are 0, defaults 40/30/30 are used.

### Safety (0–100)

Start at 100:

- Overspeed: −2 per event (cap −30)
- Harsh braking/accel: −3 per event (cap −20)

Only events with attributed `DriverId` count. Unreliable attribution → event omitted (no penalty).

### Fuel efficiency (0–100)

If liters or distance missing → **neutral 70** (insufficient data).

**With vehicle-category peer band** (≥2 drivers in same `Vehicles.VehicleType` with fuel data in period):

```
ratio = driverKmPerL / peerMedianKmPerL
≥1.15 → 100 | ≥1.05 → 90 | ≥0.95 → 80 | ≥0.85 → 65 | ≥0.75 → 50 | else 35
```

**Without peer band** (absolute km/L):

```
≥12 → 100 | ≥10 → 90 | ≥8 → 80 | ≥6 → 65 | ≥4 → 50 | else 35
```

### On-time (0–100)

Completed trips with `PlannedEnd` + `ActualEnd`:

```
onTime if ActualEnd ≤ PlannedEnd + 15 minutes
score = round(100 × onTimeCount / completedWithPlan)
```

No planned/completed trips → **neutral 70**.

### Secondary behavior score (optional / coaching)

Legacy penalty formula (overspeed, harsh, idle %, night %). **Not** the Overall used for ranking. Exposed as `secondaryBehaviorScore`.

### Rating bands

Excellent ≥90 · Good ≥75 · Fair ≥60 · Poor &lt;60

Reward: Overall ≥85 and top 20% of ranked drivers.  
Training flag: Overall &lt;60 or Safety &lt;55 or On-time &lt;50.

## Data gaps remaining

| Gap | Handling |
|---|---|
| No seatbelt / distraction sensors | Not scored (no invented data) |
| Missing fuel logs | Neutral fuel 70 |
| Missing planned/actual trip ends | Neutral on-time 70 |
| No `VehicleType` or &lt;2 peers | Absolute km/L bands |
| Traccar offline / unlinked vehicles | `IsPartial`; idle/live-harsh may be incomplete |
| Events without `DriverId` | Excluded (no unfair penalty) |
| GpsTrips lack DriverId | Attribution via `AssignmentHistory` only |

## Schema

- `DriverPerformanceScoreWeights` — per-tenant weights + `UpdatedBy`/`UpdatedAt`
- `DriverPerformanceScoreHistory` — overall/sub-scores, delta, explanation, factors/weights JSON, peer fuel metadata

## APIs

| Method | Path | Permission |
|---|---|---|
| GET | `/api/gps/analytics/drivers/ranking` | GPS analytics (`GPS.View` controller) |
| GET | `/api/gps/analytics/drivers/performance/weights` | `Driver.ViewPerformance` |
| PUT | `/api/gps/analytics/drivers/performance/weights` | `Driver.ManagePerformanceWeights` (audited) |
| GET | `/api/gps/analytics/drivers/{id}/performance` | `Driver.ViewPerformance` |
| GET | `/api/gps/analytics/drivers/{id}/performance/history` | `Driver.ViewPerformance` |
| GET | Driver app `/performance/score` | Driver identity |

Ranking recompute also appends history when Overall changes for the same period window.

## UI

ERP route: **`/gps-tracking/driver-performance`**

- Fleet ranking table (Overall, Safety, Fuel, On-time)
- Admin weight editor (when permitted)
- Driver detail: breakdown bars, trends (history), supporting overspeed/harsh events

## Acceptance criteria

1. Overall ranking uses weighted Safety/Fuel/On-time — not idle/night-primary formula.
2. Weights configurable by authorized admins; changes audited.
3. History records when/why/how much Overall changed.
4. Fuel peer bands apply when category + ≥2 peers exist.
5. Missing data → neutral scores / `IsPartial`; no penalty without attribution.
6. Unit tests cover overall blend, peer fuel, neutrals, explanation.

## Tests

`Backend/SheikhTravelSystem.Tests/GpsTracking/DriverScoreCalculatorExtendedTests.cs`
