# Phase B — Security verification results

| Field | Value |
|-------|--------|
| Date | 2026-09-30 |
| API | `https://sheikh-travel-system-production.up.railway.app` |
| Runner | `docs/production-readiness/phase-b-security-smoke.sh` |
| Account | `admin@sheikhtravel.com` (`SUPER_ADMIN`) |
| Result | **PASS=10 FAIL=0 SKIP=0** |

## Checks

| Id | Check | Result |
|----|--------|--------|
| B1 | Login | PASS |
| B2 | `/api/Auth/me` no token → 401 | PASS |
| B3 | Invalid token → 401 | PASS |
| B4 | Dashboard without `X-Tenant-Id` → 200 | PASS |
| B5 | Foreign `X-Tenant-Id` as SUPER_ADMIN → 200 (override) | PASS |
| B6 | Matching `X-Tenant-Id` → 200 | PASS |
| B7 | `/api/dev/seed` anonymous → 401 | PASS |
| B8 | `/api/lookup/timezones` → 200 | PASS |
| B9 | Portal send-otp → 200 | PASS |
| B10 | Portal `devMode=false` | PASS |

## Notes

- Tenant hard-isolation **403** follow-up (2026-09-30): `drivermanager@sheikhtravel.com` (`DRIVER_MANAGER`/`DISPATCHER`) with `X-Tenant-Id: 2` → **403**; matching tenant → **200**.
- Phase A (2026-09-30): **COMPLETE** — JWT, SQL `sa`, and Traccar passwords rotated on host + Railway; none match tracked `appsettings.json`. See [`08-go-live-status.md`](08-go-live-status.md).
- Do not commit passwords or tokens.

## Next

Phases A–D PASS. Pre-launch hygiene only — see [`08-go-live-status.md`](08-go-live-status.md).
