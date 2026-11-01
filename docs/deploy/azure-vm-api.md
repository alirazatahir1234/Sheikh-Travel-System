# SheikhGo API on Azure VM + Azure DevOps

Primary CI/CD: **Backend → Azure VM** and **Frontend → Vercel** — see [azure-vm-vercel-cicd.md](./azure-vm-vercel-cicd.md). Pipeline file: root `azure-pipelines.yml`.

## Live endpoints

| Item | Value |
|------|--------|
| **HTTPS API (clients, current)** | `https://joke-rebates-titans-duties.trycloudflare.com` (Cloudflare quick tunnel; live value also in `/var/lib/sheikhgo/cloudflared-quick-url.txt` on VM) |
| **HTTPS backup (Caddy)** | `https://20-174-1-230.sslip.io` (Let’s Encrypt; kept — some client networks see `ERR_ADDRESS_UNREACHABLE`) |
| **Preferred HTTPS host later** | `https://api.sheikhgo.com` — add DNS A `api` → `20.174.1.230`, then flip frontend env URLs (paid/custom domain — not required for tunnel) |
| HTTP API (Kestrel, kept for mobile/LAN) | `http://20.174.1.230:5082` |
| API base (HTTPS clients) | `https://joke-rebates-titans-duties.trycloudflare.com/api` |
| Health (tunnel) | `https://joke-rebates-titans-duties.trycloudflare.com/health` |
| Health (Caddy) | `https://20-174-1-230.sslip.io/health` |
| Health (HTTP) | `http://20.174.1.230:5082/health` |
| systemd units | `sheikhgo-api` (Kestrel), `caddy` (TLS proxy), `sheikhgo-cloudflared` (free quick tunnel) |
| Env file | `/etc/sheikhgo/api.env` (chmod 600) |
| Releases | `/opt/sheikhgo-api/releases/<stamp>` → `/opt/sheikhgo-api/current` |
| Swap | 2G `/swapfile` |
| SQL | local Docker `sheikhgo-sql` on `127.0.0.1:1433` (untouched) |
| Traccar | local `127.0.0.1:8082` (untouched) |
| Railway | **left intact** (public Railway app may be offline; Azure is primary) |

## HTTPS / Mixed Content (ERP on Vercel)

`https://www.sheikhgo.com` must call an **HTTPS** API (never `http://20.174.1.230:5082`).

### Cloudflare quick tunnel (free, current clients)

Free `*.trycloudflare.com` tunnel to Kestrel — **no paid domain**.

| Item | Value |
|------|--------|
| Unit | `sheikhgo-cloudflared` |
| Wrapper | `/usr/local/bin/sheikhgo-cloudflared-quick.sh` |
| Current URL file | `/var/lib/sheikhgo/cloudflared-quick-url.txt` |
| Target | `http://127.0.0.1:5082` |
| Clients | ERP `environment.prod.ts` + Flutter `AppConfig.defaultProductionApiBaseUrl` |

```bash
# Install (once) + start
# cloudflared from GitHub .deb; systemd enables sheikhgo-cloudflared
sudo systemctl status sheikhgo-cloudflared
cat /var/lib/sheikhgo/cloudflared-quick-url.txt
curl -sS "$(cat /var/lib/sheikhgo/cloudflared-quick-url.txt)/health"
```

**Caveat:** quick-tunnel URLs usually change whenever `sheikhgo-cloudflared` restarts. After a restart, update ERP + Flutter prod URLs to the new host and redeploy. Caddy/sslip.io remains as backup (some networks get `ERR_ADDRESS_UNREACHABLE` to the Azure public IP).

### How Ali updates the API URL after a tunnel restart

1. SSH to the VM and read the live host:
   ```bash
   ssh -i ~/.ssh/sheikhgo_vm_cicd azureuser@20.174.1.230 \
     'cat /var/lib/sheikhgo/cloudflared-quick-url.txt'
   curl -sS "$(cat that-url)/health"   # expect Healthy
   ```
2. Set the same origin (plus `/api`) in:
   - `Frontend/sheikhgo-erp/src/environments/environment.prod.ts` → `apiUrl`
   - `Frontend/sheikh-travel-customer-hub/src/environments/environment.prod.ts` → `apiRoot` / `hubRoot`
   - `Frontend/sheikh-driver/lib/core/config/app_config.dart` → `defaultProductionApiBaseUrl`
   - `Frontend/sheikh-driver/scripts/build_prod.sh` → `DEFAULT_PROD_API`
3. Redeploy ERP/Hub to Vercel (`deploy/vercel/deploy-frontends.sh` or ADO frontend job) and rebuild mobile release APKs.
4. Local `ng serve` (`environment.ts` → `/api` proxy to `:5082`) is unchanged — do not point localhost ERP at the tunnel unless testing prod.

Named Cloudflare tunnels with a stable hostname require a zone in a Cloudflare account (still free plan possible) — not configured here.

### Caddy / sslip.io (backup, kept)

Caddy terminates TLS on **:443** and proxies to Kestrel **:5082**.

```bash
chmod +x deploy/azure-vm/install-caddy-tls.sh
./deploy/azure-vm/install-caddy-tls.sh
```

Azure NSG must allow **80** (ACME) and **443** (HTTPS). Rules: `Allow-HTTP-80`, `Allow-HTTPS-443`.

### Switch to `api.sheikhgo.com` (optional later)

1. Spaceship DNS → A record: `api` → `20.174.1.230` (TTL 300).
2. Wait for `dig +short api.sheikhgo.com` → `20.174.1.230`.
3. Caddy auto-issues the cert (same Caddyfile already lists the host).
4. Update ERP / Hub / Flutter prod URLs → `https://api.sheikhgo.com`.
5. Redeploy ERP to Vercel.

## Azure DevOps self-hosted agent on the VM

Root `azure-pipelines.yml` Backend job uses pool **`SheikhGo-VM`** (agent on `20.174.1.230`). Frontend job uses Microsoft-hosted `ubuntu-latest` → Vercel.

Flow:

1. Push to `main`/`master` (paths under `Backend/**`, etc.)
2. Backend job runs on pool **`SheikhGo-VM`**
3. `deploy/azure-vm/deploy-api.sh` runs `dotnet publish` + `systemctl restart sheikhgo-api`

### What Ali must click once (agent + secrets)

1. **Project settings → Agent pools → Add pool** named exactly **`SheikhGo-VM`** (self-hosted), if missing.
2. Create a PAT: User settings → Personal access tokens → **Agent Pools (Read & manage)** + **Code (Read)** if needed. Copy once; do not commit.
3. On the VM (SSH as `azureuser`):

```bash
cd /opt/sheikhgo-api/src/deploy/azure-vm   # after repo is on the VM
# or copy install-ado-agent.sh from the repo
export AZP_URL='https://dev.azure.com/Alirazatahir'
export AZP_TOKEN='***PAT***'
export AZP_POOL='SheikhGo-VM'
export AZP_AGENT_NAME='sheikhgo-vm'
chmod +x install-ado-agent.sh
./install-ado-agent.sh
unset AZP_TOKEN
```

4. **Pipelines → New pipeline → Azure Repos Git → SheikhGo** → **Existing Azure Pipelines YAML** → `/azure-pipelines.yml`.
5. Add Vercel variables (see [azure-vm-vercel-cicd.md](./azure-vm-vercel-cicd.md)).
6. Run once (manual) or push under `Backend/` / frontend paths to verify.

### How to trigger / verify

- **Trigger:** `git push` to `main`/`master` (with matching path changes), or **Run pipeline** in Azure DevOps.
- **Verify tunnel:** `curl -sS "$(cat /var/lib/sheikhgo/cloudflared-quick-url.txt)/health"` → plain text `Healthy`
- **Verify Caddy HTTPS:** `curl -s https://20-174-1-230.sslip.io/health` → plain text `Healthy`
- **Verify HTTP (kept):** `curl -s http://20.174.1.230:5082/health` → plain text `Healthy`
- **Liveness:** `curl -s http://20.174.1.230:5082/health/live` → plain text `Healthy` (no deps)
- **Detail (JSON):** `curl -s http://20.174.1.230:5082/health/detail` → JSON `"status":"Healthy"` with per-check entries
- **Readiness:** `curl -s -o /dev/null -w '%{http_code}' http://20.174.1.230:5082/health/ready` → `200` (or `503` if SQL/Redis/Traccar degraded)
- **Logs:** `journalctl -u sheikhgo-api -f` / `journalctl -u caddy -f` / `journalctl -u sheikhgo-cloudflared -f`
- **Agent:** Azure DevOps → Agent pools → SheikhGo-VM → agent online

### Manual deploy (no ADO)

```bash
ssh azureuser@20.174.1.230
/opt/sheikhgo-api/deploy/deploy-api.sh
# or from repo checkout:
DEPLOY_BRANCH=main /opt/sheikhgo-api/src/deploy/azure-vm/deploy-api.sh
```

## Caveats

- **CORS** already allows `https://www.sheikhgo.com` / `https://sheikhgo.com` and `*.sheikhgo.com` (no change required for sslip.io API host — CORS checks the browser Origin, not the API hostname).
- **RAM:** ~4 GB VM with SQL + Traccar + API + Caddy; swap in use under load. Live unit uses `MemoryMax=512M` / `MemoryHigh=400M` (not managed by `deploy-api.sh` — unit lives on the VM only).
- **Memory (Ali approve before changing live):** if swap stays high after worker/GPS fixes, consider a conservative bump, e.g. `MemoryMax=768M` + `MemoryHigh=600M` in `/etc/systemd/system/sheikhgo-api.service`, then `sudo systemctl daemon-reload && sudo systemctl restart sheikhgo-api`. Do **not** raise past ~1G without more host RAM or moving SQL/Traccar off-box. App-side mitigations (tighter history-replay default window + lower route/playback point caps) ship in code.
- **Do not** restart/reconfigure Traccar or `sheikhgo-sql` for API deploys.
- **Do not** pause/delete Railway until you explicitly choose to.
