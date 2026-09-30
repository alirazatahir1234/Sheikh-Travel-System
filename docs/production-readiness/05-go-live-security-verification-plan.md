# Go-Live Plan — Security Verification Gate

| Field | Value |
|-------|--------|
| Status | **READY TO EXECUTE** |
| Prerequisite | Package 1 Phases 0–3C + prod tenant diagnostic COMPLETE |
| Production API | `https://sheikh-travel-system-production.up.railway.app` |
| Production DB | Railway → `SheikhGo` @ `20.174.1.230` (confirmed CLEAN isolation) |
| You are logged in | Cursor (Railway / host access assumed available) |
| Out of scope | Phase 4 · DEFAULT 1 removal · data repair · MCP/AI · UI polish |

---

## Goal

Prove production is safe enough for **one real customer** to run the critical fleet path end-to-end without developer intervention.

```text
Rotate secrets → Redeploy healthy → Security smoke → Critical E2E trip → Driver Sprint 1
```

---

## Phase A — Rotate production secrets (P0 · do first)

**Why:** Credentials were previously in git history / tracked config. Scrubbing the repo does **not** rotate live credentials.

### A1. Generate new values (offline / password manager)

| # | Secret | Notes |
|---|--------|--------|
| 1 | SQL Server login password | Change on SQL host first, then update connection string |
| 2 | `JwtSettings__Secret` | Random **≥ 32** chars; **invalidates all existing JWTs** (users re-login) |
| 3 | `Traccar__Password` | Match new Traccar UI / API user password |
| 4 | Google Maps server key | Regenerate in Cloud Console; restrict by server IP / API |

### A2. Set Railway variables (production service)

Service: **Sheikh-Travel-System** (or linked `sheikh-travel-backend-api` production).

| Environment variable | Must be set |
|----------------------|-------------|
| `ConnectionStrings__DefaultConnection` | Full SQL connection string with **new** password |
| `JwtSettings__Secret` | New ≥32-char secret |
| `Traccar__Password` | New Traccar password (if `Traccar__Enabled=true`) |
| `GoogleMaps__ApiKey` **or** `Geocoding__GoogleMapsApiKey` | New Maps key |

Optional (confirm current values; do not weaken):

| Variable | Recommended for go-live |
|----------|-------------------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PortalAuth__DevMode` | unset or `false` (ignored in Production anyway) |
| `MultiTenancy__AllowAnonymousDefaultTenant` | leave `true` for now (do **not** flip in this plan) |

Reference: [`Backend/README.secrets.md`](../../Backend/README.secrets.md)

### A3. Redeploy / restart

1. Trigger Railway redeploy (or restart after variable change).
2. Confirm container starts (no `ProductionSecretsValidator` crash).
3. Hit health / login endpoint — API responds.

### A4. Pass criteria

- [ ] API process stays up after restart  
- [ ] Login with a known ERP user succeeds (expect re-login after JWT rotation)  
- [ ] Traccar sync still receives positions (if GPS required for E2E)  
- [ ] Maps geocoding / maps-backed API call still works  

**STOP if API fails to start** — fix missing/placeholder env vars before continuing. Do not proceed to Phase B.

---

## Phase B — Security verification smoke (P0)

Run against **production** URL. Record pass/fail only — no code changes in this phase unless a real P0 bug appears.

**Ready-made requests:**

| File | Use |
|------|-----|
| [`phase-b-security-smoke.sh`](phase-b-security-smoke.sh) | One-shot curl runner (prints PASS/FAIL) |
| [`phase-b-security-smoke.http`](phase-b-security-smoke.http) | VS Code / Cursor REST Client |

```bash
export SMOKE_EMAIL='your-erp-user@example.com'
export SMOKE_PASSWORD='...'          # do not paste into chat
export SMOKE_PORTAL_PHONE='+92...'   # optional
chmod +x docs/production-readiness/phase-b-security-smoke.sh
./docs/production-readiness/phase-b-security-smoke.sh
```

Requires `curl` + `jq`. Default base URL is the Railway production API.

### B1. Authentication

| # | Check | Expected |
|---|--------|----------|
| 1 | `POST` login with valid credentials | 200 + JWT containing `tenant_id` |
| 2 | Call a protected ERP endpoint **without** token | 401 |
| 3 | Call with expired/invalid token | 401 |

### B2. Tenant isolation (Package 1 Phase 1)

Use a normal (non–`SUPER_ADMIN`) user whose JWT `tenant_id` = **1**.

| # | Check | Expected |
|---|--------|----------|
| 4 | Authenticated request **without** `X-Tenant-Id` | Uses JWT tenant; data scoped to tenant 1 |
| 5 | Same request with `X-Tenant-Id: 2` | **403 Forbidden** |
| 6 | Same request with matching `X-Tenant-Id: 1` | 200 |

### B3. Dev / public surface (Package 1 Phase 2)

| # | Check | Expected |
|---|--------|----------|
| 7 | `GET /api/dev/...` anonymous | 401 (or 404 outside Development) |
| 8 | `GET /api/lookup/timezones` anonymous | 200 (PublicByDesign) + rate limit applies |

### B4. PortalAuth

| # | Check | Expected |
|---|--------|----------|
| 9 | Request portal OTP on Production | SMS path (or configured SMS); **not** fixed Dev OTP |
| 10 | Confirm `ASPNETCORE_ENVIRONMENT=Production` on host | DevMode bypass impossible even if mis-set in config |

### B5. Pass criteria

- [ ] All B1–B4 checks pass  
- [ ] Soft-delete orphans from [`04-production-tenant-data-diagnostic.md`](04-production-tenant-data-diagnostic.md) accepted as **non-blocking hygiene**  

**STOP if tenant 403 or Dev endpoints are open** — treat as P0; fix before E2E.

---

## Phase C — Critical end-to-end dry-run (P0)

One real path on production (tenant **Sheikh Travel** / id 1 is the data-rich tenant).

```text
ERP login
  → Vehicle exists / usable
  → Driver exists / usable
  → Create booking or trip
  → Assign vehicle + driver
  → Driver receives trip (app or API)
  → Driver starts trip
  → GPS / live tracking visible in ERP
  → Driver completes trip
  → ERP shows completed + persisted
```

### C1. Pass criteria

- [ ] Trip completes without manual SQL or developer hotfix  
- [ ] Live GPS visible during active trip (or known Traccar device online)  
- [ ] Completion visible in ERP after refresh  

**STOP if start/complete/GPS fail** — that is a go-live blocker; fix before Driver Sprint 1 expansion.

---

## Phase D — Driver app Sprint 1 (P0/P1 · after C)

Use [`Frontend/sheikh-driver/docs/production_readiness_checklist.md`](../../Frontend/sheikh-driver/docs/production_readiness_checklist.md).

Minimum for first customer:

- [ ] Accept / Arrived / Onboard / Complete on **one** real device OS  
- [ ] Continue Trip opens navigation  
- [ ] Offline action replay (if used in field)  
- [ ] GPS batch to `/driver-app/location/batch`  

Payments Sprint 2+ can follow if cash collection is required on day one.

---

## Phase E — Explicitly NOT in this plan

Do **not** start until Phases A–C pass and product approves:

| Item | Reason |
|------|--------|
| Phase 4 workflow gaps | Documented deferred |
| Remove SQL `DEFAULT 1` on TenantId | Residual risk; needs separate approval |
| Data repair / orphan cleanup | Human decision; no auto UPDATE |
| ADR-009 `appsettings.json` placeholder scrub | Separate override merge after rotation |
| Flip `AllowAnonymousDefaultTenant=false` | Transitional default; change later |
| MCP / AI | Not a first-customer blocker |

---

## Execution order (checklist)

| Step | Owner | Done |
|------|--------|------|
| A1 Generate new secrets | Cursor | [x] JWT + SQL + Traccar rotated 2026-09-30 — see `08-go-live-status.md` |
| A2 Set Railway env vars | Cursor | [x] Connection string, JWT, Traccar password/BaseUrl/Username/Enabled; `ASPNETCORE_ENVIRONMENT=Production` |
| A3 Redeploy + confirm healthy | Cursor | [x] Online; ERP + driver login OK after rotation |
| B1–B4 Security smoke | You + Cursor assist | [x] PASS 2026-09-30 — see `05-security-verification-results.md` |
| C Critical E2E trip | You + Cursor | [x] PASS — `06-phase-c-e2e-results.md` |
| D Driver Sprint 1 minimum | Cursor | [x] PASS — `07-phase-d-driver-app-walkthrough.md` |
| Write results into `docs/production-readiness/05-security-verification-results.md` | After pass | [x] + `08-go-live-status.md` |

---

## How Cursor helps (while you are logged in)

1. **You** set/rotate Railway secrets (Cursor must not paste live passwords into chat or commits).  
2. Ask Cursor to **draft the exact HTTP smoke requests** (curl/HTTP files) for Phase B against the production URL.  
3. Ask Cursor to **walk ERP screens / API sequence** for Phase C once API is healthy.  
4. Ask Cursor to **triage failures** from smoke/E2E with logs — still no Phase 4 / DEFAULT 1 unless you explicitly approve.

---

## Definition of done for this plan

Production secrets rotated and bound via Railway env · security smoke green · one completed trip with GPS · Driver Sprint 1 minimum checked · results documented · Phase 4 still not started.
