# Go-Live Status — 2026-09-30

| Field | Value |
|-------|--------|
| Plan | [`05-go-live-security-verification-plan.md`](05-go-live-security-verification-plan.md) |
| Production API | `https://sheikh-travel-system-production.up.railway.app` · **Online** |
| ERP | `https://www.sheikhgo.com` |

## GO-LIVE STATUS

```text
Web/ERP:       READY (Phase C PASS)
Backend:       READY (API healthy after secret rotation)
Driver App:    READY (Phase D Sprint 1 PASS — Android + API lifecycle)
GPS/Traccar:   READY (Live Map proven; Traccar password rotated + verified)
Database:      READY (SQL `sa` password rotated; Railway connection string updated)
Security:      READY (JWT + SQL + Traccar rotated 2026-09-30; none match tracked appsettings)
Deployment:    READY (Railway production Online; ASPNETCORE_ENVIRONMENT=Production)
MCP/AI:        DEFERRED (not a first-customer blocker)
```

## Phase checklist

| Phase | Result | Evidence |
|-------|--------|----------|
| A — Secrets | **PASS** | Host SQL + Traccar passwords rotated; Railway `ConnectionStrings__DefaultConnection`, `Traccar__Password`, `JwtSettings__Secret` updated; `ASPNETCORE_ENVIRONMENT=Production`; `Traccar__BaseUrl` / `Username` / `Enabled` set on Railway. Post-redeploy: ERP + driver login OK; `/vehicles` 200. Hash check: SQL / Traccar / JWT **≠** tracked `appsettings.json`. |
| B — Security smoke | **PASS** | `05-security-verification-results.md` (10/10). Follow-up: non–`SUPER_ADMIN` `X-Tenant-Id:2` → **403**. |
| C — Critical E2E | **PASS** | `06-phase-c-e2e-results.md` + BK-2026-2010 completed in Phase D |
| D — Driver Sprint 1 | **PASS** | `07-phase-d-driver-app-walkthrough.md` Arrived → Onboard → Complete |

## Blocking Issues (pre-first-customer)

None for the critical fleet path. Remaining items are hygiene / deferred (below).

## Pre-Launch Tasks (non-blocking hygiene)

- Confirm Google Maps key was regenerated in Cloud Console (Railway value ≠ tracked file; restriction by server IP recommended).
- Optional: emulator deep-link smoke for Continue Trip → Google Maps.
- Offline action replay (field use) — deferred if not day-one.
- Store new SQL + Traccar passwords in the team password manager (not in git / chat).
- Tracked `appsettings.json` still contains **old** placeholders — ADR-009 scrub later; Railway env overrides win.

## Post-Launch / Explicitly deferred

- Phase 4 workflow gaps
- Remove SQL `DEFAULT 1` on TenantId
- Soft-delete orphan cleanup
- ADR-009 tracked `appsettings.json` placeholder scrub
- Flip `AllowAnonymousDefaultTenant=false`
- MCP / AI expansion
- Driver Sprint 2 payments / iOS matrix / store tracks

## Phase A validation (done)

1. SQL `sa` password changed on `20.174.1.230` → Railway connection string updated → ERP + driver login OK.
2. Traccar user `info@sheikhgo.com` password changed via API → `Traccar__Password` updated on Railway → login verified against Traccar.
3. JWT previously rotated; API healthy after redeploy.
