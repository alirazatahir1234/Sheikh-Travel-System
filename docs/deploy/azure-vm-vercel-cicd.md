# SheikhGo CI/CD — Backend → Azure VM · Frontend → Vercel

Org **Alirazatahir** · Project **SheikhGo** · Pipeline file: `azure-pipelines.yml`

**Production pipeline:** **`SheikhGo-Deploy-GitHub`** only (GitHub repo `alirazatahir1234/Sheikh-Travel-System`).  
The older **`SheikhGo-Deploy`** definition (Azure Repos mirror `SheikhGo`) is **disabled** — that mirror lagged GitHub `main` by ~226 commits and overwrote production (2026-11-12 run 141 @ `ff92a22a` after GitHub run 140 @ `757fe534`).

| Target | Trigger paths | How |
|--------|---------------|-----|
| **Azure VM** `20.174.1.230` (API) | `Backend/**`, `deploy/azure-vm/**` | Self-hosted pool `SheikhGo-VM` runs `deploy/azure-vm/deploy-api.sh` → `dotnet publish` + `systemctl restart sheikhgo-api` |
| **Vercel** project **sheikh-travel-system** → `www.sheikhgo.com` | `Frontend/sheikhgo-erp/**`, `Frontend/sheikh-travel-customer-hub/**` | `deploy/vercel/deploy-frontends.sh` via Vercel CLI on self-hosted pool `SheikhGo-VM` |

**Not** this pipeline’s target: Railway (left intact).

Trigger branches: **main** and **master** (`batch: true`). Deploy job refuses Azure Repos provider and refuses SourceVersions that are behind `origin/main` tip. Manual recovery: run **SheikhGo-Deploy-GitHub** with **Force full deploy** checked.

Live API HTTPS: `https://joke-rebates-titans-duties.trycloudflare.com` (Cloudflare quick tunnel → Kestrel) · Caddy backup: `https://20-174-1-230.sslip.io` · HTTP kept: `http://20.174.1.230:5082` · units: `sheikhgo-api`, `caddy`, `sheikhgo-cloudflared` · health: `/health`  
Preferred host after DNS: `https://api.sheikhgo.com` — see [azure-vm-api.md](./azure-vm-api.md).

---

## Backend (Azure VM) — one-time agent

The Backend job uses pool **`SheikhGo-VM`**. The agent must run **on** `20.174.1.230` so build + restart happen locally (no SSH secrets in ADO).

1. ADO → **Project settings → Agent pools → Add pool** → name exactly **`SheikhGo-VM`** (self-hosted).
2. Create a PAT: User settings → Personal access tokens → scopes **Agent Pools (Read & manage)** (+ **Code (Read)** if needed).
3. On the VM as `azureuser`:

```bash
export AZP_URL='https://dev.azure.com/Alirazatahir'
export AZP_TOKEN='***PAT***'
export AZP_POOL='SheikhGo-VM'
export AZP_AGENT_NAME='sheikhgo-vm'
cd /path/to/repo/deploy/azure-vm   # or copy install-ado-agent.sh onto the VM
chmod +x install-ado-agent.sh
./install-ado-agent.sh
unset AZP_TOKEN
```

4. Confirm agent is **online**: Project settings → Agent pools → SheikhGo-VM.

---

## Vercel variables (ADO Library or pipeline Variables)

| Variable | Where to get it |
|----------|-----------------|
| `VERCEL_TOKEN` | Vercel → Settings → Tokens (**secret**) |
| `VERCEL_ORG_ID` | Project → Settings → General |
| `VERCEL_ERP_PROJECT_ID` | sheikhgo-erp project settings |
| `VERCEL_HUB_PROJECT_ID` | customer-hub project settings (optional; `PLACEHOLDER` skips hub so ERP still deploys) |
| `VERCEL_PROD` | `true` (default) |
| `GOOGLE_MAPS_API_KEY` | SheikhGo-Frontend **browser** key (**secret**) — injected at build by `Frontend/sheikhgo-erp/scripts/inject-maps-env.mjs` into `environment.prod.ts`, then placeholders restored. Never commit the live value. |
| `GOOGLE_MAPS_MAP_ID` | Optional Map ID for Advanced Markers |

Also set the same `GOOGLE_MAPS_*` names on the **Vercel project → Settings → Environment Variables** (Production) so remote `vercel build` / Hobby rebuilds see them. `deploy/vercel/deploy-frontends.sh` additionally writes them into `.vercel/.env.production.local` before prebuilt deploy.

**HTTP referrer restrictions** (Google Cloud Console → Credentials → this browser key) — required; browser keys are visible in the SPA by design:

- `https://www.sheikhgo.com/*`
- `https://sheikhgo.com/*`
- `http://localhost:4200/*`
- `http://127.0.0.1:4200/*`

Enable APIs on the same GCP project: Maps JavaScript API, Places API, Geocoding API, Routes API. Billing must be linked for live traffic — the ERP only blames billing when Google’s own error URL/copy says so.

**Local / laptop builds:** prefer `Frontend/sheikhgo-erp/.env.local` (from `.env.example`), or gitignored `secrets/local.env`, or `environment.secrets.local.ts` (from the `.example`). `npm start` / `build:prod` run inject automatically.

Suggested Library variable group name: **`SheikhGo-Deploy`**. Link it to the pipeline.

---

## Create / wire the pipeline

1. Open https://dev.azure.com/Alirazatahir/SheikhGo/_git/SheikhGo — ensure `azure-pipelines.yml` is on `main`.
2. **Pipelines → New pipeline → Azure Repos Git → SheikhGo**
3. **Existing Azure Pipelines YAML file** → `/azure-pipelines.yml`
4. Add Vercel variables (above); mark secrets.
5. Ensure pool `SheikhGo-VM` has an online agent.
6. Save; push to `main`/`master` (matching paths) or **Run pipeline**.

---

## Verify

- Backend HTTPS (tunnel): `curl -s https://joke-rebates-titans-duties.trycloudflare.com/health` → plain text `Healthy`
- Backend HTTPS (Caddy backup): `curl -s https://20-174-1-230.sslip.io/health` → plain text `Healthy`
- Backend HTTP (kept): `curl -s http://20.174.1.230:5082/health` → plain text `Healthy`
- Detail JSON: `curl -s http://20.174.1.230:5082/health/detail` → `"status":"Healthy"`
- Readiness: `curl -s -o /dev/null -w '%{http_code}' http://20.174.1.230:5082/health/ready` → `200` (503 if deps degraded)
- Logs: `journalctl -u sheikhgo-api -f` on the VM
- Frontend: Vercel dashboard deployment for ERP / customer-hub
