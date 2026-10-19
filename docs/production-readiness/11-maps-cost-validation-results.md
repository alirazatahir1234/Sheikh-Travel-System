# Maps cost-cut validation results

| Field | Value |
|-------|--------|
| Date | 2026-09-30 |
| Decision | Keep Google Maps; no Azure migration |
| Cost-cut commit | `e6c0dc0a` |
| Target | ~$0–$20/month Maps spend for ~10 vehicles |

## 1. Railway deploy — PASS

| Check | Result |
|-------|--------|
| Prod service | Online · `https://sheikh-travel-system-production.up.railway.app` |
| Active deployment | `70a0fbf7-…` SUCCESS (2026-09-30 23:05 +05) |
| Git commit on deploy | **`e6c0dc0ab5c03d7650cb5b77191b8f529e949a73`** (cost-cut) |
| Health | `GET /health` → 200 `Healthy` |
| `Geocoding__PreferGoogle` | **unset** → class default `false` |
| `Geocoding__IncludeNearbyPlace` | **unset** → class default `false` |
| `Geocoding__BackfillCooldownoldownSeconds` | **unset** → class default `300` |
| Maps keys on Railway | `Geocoding__GoogleMapsApiKey` + `GoogleMaps__ApiKey` set (not logged) |

Do **not** set PreferGoogle / IncludeNearbyPlace to true in Railway.

## 2. Google Cloud quotas + budgets

| Check | Result |
|-------|--------|
| Project | `techbirdsfly` |
| `gcloud` CLI | **Blocked** — `invalid_grant` (needs `gcloud auth login`) |
| Console automation | Quotas page opened but gstatic JS blocked in agent browser |
| Script prepared | [`scripts/apply-maps-quotas-and-budgets.sh`](scripts/apply-maps-quotas-and-budgets.sh) |

**Ali — one-time (~10 min) in Google Cloud Console (logged-in browser):**

1. Quotas: [Maps quotas · techbirdsfly](https://console.cloud.google.com/google/maps-apis/quotas?project=techbirdsfly)
   - Geocoding API → Requests/day → **2000** (start; raise if UI needs more)
   - Places API (legacy Nearby Search) → Requests/day → **500**
2. Budgets: [Billing budgets](https://console.cloud.google.com/billing/budgets?project=techbirdsfly)
   - Budget **SheikhGo Maps $20** (threshold alerts 50/90/100%)
   - Budget **SheikhGo Maps $50**
3. Or after `gcloud auth login`:

```bash
export GCP_MAPS_PROJECT=techbirdsfly
export GCP_BILLING_ACCOUNT=<from: gcloud billing accounts list>
./docs/production-readiness/scripts/apply-maps-quotas-and-budgets.sh
```

## 3. Production API smoke — PASS (2026-09-30)

Authenticated as ERP admin against production (token not retained).

| Call | Result |
|------|--------|
| `GET /api/gps/live` | 200 · vehicle **50** online · address from VCL (`…Pasrur, Punjab…`) |
| `GET /api/gps/location/reverse?lat=&lng=` (no forceRefresh) | 200 · **`fromCache: true`** · street line returned |
| `GET /api/gps/history/50` (~6h) | 200 · **1414** points (no client forceRefresh) |
| `GET /api/gps/maps/nearby-places?…&category=fuel` | 200 · Places New returns fuel POIs (**explicit** call only) |

Interpretation: live roster serves stored address; reverse hits cache; Nearby only when that endpoint is called (panel expand path). No evidence of Places pairing on reverse.

## 4. Seven-day metrics watch

| Day | Date | Geocoding req/day | Places Nearby (legacy) | Notes |
|-----|------|-------------------|------------------------|-------|
| 0 (baseline) | 2026-09-30 | **0** (Console Quotas “Current usage” for Geocoding v3/day at check time) | _confirm Places SKU in Metrics_ | Cost-cut live on Railway; Geocoding day quota still **Unlimited** until Console edit |
| 1 | 2026-10-01 | | | |
| 2 | 2026-10-02 | | | |
| 3 | 2026-10-03 | | | |
| 4 | 2026-10-04 | | | |
| 5 | 2026-10-05 | | | |
| 6 | 2026-10-06 | | | |
| 7 | 2026-10-07 | | | Decide: stay Google vs evaluate Azure |

**Where to look:** [Maps Metrics](https://console.cloud.google.com/google/maps-apis/metrics?project=techbirdsfly) · Billing → Reports → group by SKU.

**Pass criteria (7 days):** Geocoding + legacy Nearby Search sharply below prior week; monthly run-rate trending toward **$0–$20**. Escalate to Azure / provider abstraction only if still high after day 7 on this build.

## Checklist

- [x] Railway on `e6c0dc0a+` with cost-safe Geocoding defaults
- [ ] GCP daily quotas set (Console / script — Ali after `gcloud auth login`)
- [ ] GCP $20 / $50 budget alerts set
- [x] Live + reverse + history smoke
- [x] Nearby panel API still works when called explicitly
- [ ] Fill 7-day metrics table (start today)
