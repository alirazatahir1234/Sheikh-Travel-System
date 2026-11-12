# SheikhGo API on Azure VM + Azure DevOps

Primary CI/CD: **Backend → Azure VM** and **Frontend → Vercel** — see [azure-vm-vercel-cicd.md](./azure-vm-vercel-cicd.md). Pipeline file: root `azure-pipelines.yml`.

## Live endpoints

| Item | Value |
|------|--------|
| **Mobile / Fleet API (current)** | `http://20.174.1.230:5082/api` (Kestrel cleartext; Android NSC + iOS ATS allowlisted) |
| **HTTPS API (browsers / ERP / Hub)** | `https://20-174-1-230.sslip.io` (Caddy + Let’s Encrypt; SAN is **only** this hostname — do **not** use `https://20.174.1.230`) |
| **Preferred HTTPS host later** | `https://api.sheikhgo.com` — add DNS A `api` → `20.174.1.230`, then flip client URLs (Caddyfile already lists the host) |
| HTTP health | `http://20.174.1.230:5082/health` |
| HTTPS health (Caddy) | `https://20-174-1-230.sslip.io/health` |
| **Deprecated** | `*.trycloudflare.com` quick tunnels — currently **NXDOMAIN**; do not bake into APKs or `environment.prod.ts` |
| systemd units | `sheikhgo-api` (Kestrel), `caddy` (TLS proxy); `sheikhgo-cloudflared` optional/legacy |
| Env file | `/etc/sheikhgo/api.env` (chmod 600) |
| Releases | `/opt/sheikhgo-api/releases/<stamp>` → `/opt/sheikhgo-api/current` |
| Swap | 2G `/swapfile` |
| SQL | local Docker `sheikhgo-sql` published as `127.0.0.1:1433:1433` only (never `0.0.0.0:1433`) |
| Traccar | local `127.0.0.1:8082` (untouched) |
| Railway | **left intact** (public Railway app may be offline; Azure is primary) |

## HTTPS / Mixed Content (ERP on Vercel)

`https://www.sheikhgo.com` must call an **HTTPS** API (never `http://20.174.1.230:5082`). ERP stays on Caddy/sslip (or `api.sheikhgo.com` once DNS is live).

### Azure NSG (SheikhGo-VM-nsg) — verified 2026-11-12

Inbound allows already present (no rule changes required for this fix):

| Rule | Port | Source | Access |
|------|------|--------|--------|
| `Allow-SheikhGo-API-5082` | 5082 | Internet | Allow |
| `Allow-HTTPS-443` | 443 | * | Allow |
| `Allow-HTTP-80` | 80 | * | Allow |
| `Deny-SQL-1433-Internet` | 1433 | Internet | Deny |

From an external probe, TCP **80 / 443 / 5082** to `20.174.1.230` succeed. If some mobile carriers still report `ERR_ADDRESS_UNREACHABLE` to the Azure public IP, that is **not** an NSG gap — prefer a Cloudflare **named** tunnel or `api.sheikhgo.com` proxied via CDN (not configured in-repo yet).

### Cloudflare quick tunnel (legacy — do not ship)

Free `*.trycloudflare.com` tunnels die when `cloudflared` restarts (hostname becomes NXDOMAIN). Historical host `joke-rebates-titans-duties.trycloudflare.com` is dead. Mobile `build_prod.sh` and Vercel deploy rewrite **refuse** trycloudflare bake-ins.

Named Cloudflare tunnels with a stable hostname require a zone in a Cloudflare account — not configured here.

### Caddy / sslip.io (browser HTTPS)

Caddy terminates TLS on **:443** and proxies to Kestrel **:5082**. Cert SAN: `DNS:20-174-1-230.sslip.io` only.

```bash
chmod +x deploy/azure-vm/install-caddy-tls.sh
./deploy/azure-vm/install-caddy-tls.sh
```

Azure NSG must allow **80** (ACME) and **443** (HTTPS). Rules: `Allow-HTTP-80`, `Allow-HTTPS-443`.

### Switch to `api.sheikhgo.com` (recommended next)

1. Spaceship DNS → A record: `api` → `20.174.1.230` (TTL 300).
2. Wait for `dig +short api.sheikhgo.com` → `20.174.1.230`.
3. Caddy auto-issues the cert (same Caddyfile already lists the host).
4. Update ERP / Hub / Flutter prod URLs → `https://api.sheikhgo.com`.
5. Redeploy ERP to Vercel; rebuild mobile with HTTPS base URL.

## Azure DevOps self-hosted agent on the VM

Root `azure-pipelines.yml` Backend job uses pool **`SheikhGo-VM`** (agent on `20.174.1.230`). Frontend job uses Microsoft-hosted `ubuntu-latest` → Vercel.

Flow:

1. Push to `main`/`master` (paths under `Backend/**`, etc.)
2. Backend job runs on pool **`SheikhGo-VM`**
3. `deploy/azure-vm/deploy-api.sh` runs `dotnet publish` + `systemctl restart sheikhgo-api`

### What Ali must click once (agent + secrets)

1. **Project settings → Agent pools → Add pool** named exactly **`SheikhGo-VM`** (self-hosted), if missing.
2. Create a PAT with Agent Pools (Read & manage).
3. Run `deploy/azure-vm/install-ado-agent.sh` on the VM.
4. Link Library variable group **`SheikhGo-Deploy`** (Vercel + Maps secrets).

## Verify

- **Mobile / HTTP:** `curl -s http://20.174.1.230:5082/health` → plain text `Healthy`
- **Verify Caddy HTTPS:** `curl -s https://20-174-1-230.sslip.io/health` → plain text `Healthy`
- **Liveness:** `curl -s http://20.174.1.230:5082/health/live` → plain text `Healthy` (no deps)
- **Detail (JSON):** `curl -s http://20.174.1.230:5082/health/detail` → JSON `"status":"Healthy"` with per-check entries
- **Readiness:** `curl -s -o /dev/null -w '%{http_code}' http://20.174.1.230:5082/health/ready` → `200` (or `503` if SQL/Redis/Traccar degraded)

```bash
ssh azureuser@20.174.1.230
sudo systemctl status sheikhgo-api caddy
journalctl -u sheikhgo-api -n 80 --no-pager
```

- **CORS** already allows `https://www.sheikhgo.com` / `https://sheikhgo.com` and `*.sheikhgo.com` (CORS checks the browser Origin, not the API hostname).
