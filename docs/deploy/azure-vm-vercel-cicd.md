# SheikhGo CI/CD — Backend → Azure VM · Frontend → Vercel

Org **Alirazatahir** · Project **SheikhGo** · Repo **SheikhGo** · Pipeline file: `azure-pipelines.yml`

| Target | Trigger paths | How |
|--------|---------------|-----|
| **Azure VM** `20.174.1.230` (API) | `Backend/**`, `deploy/azure-vm/**` | Self-hosted pool `SheikhGo-VM` runs `deploy/azure-vm/deploy-api.sh` → `dotnet publish` + `systemctl restart sheikhgo-api` |
| **Vercel** (ERP + Customer Hub) | `Frontend/sheikhgo-erp/**`, `Frontend/sheikh-travel-customer-hub/**` | `deploy/vercel/deploy-frontends.sh` via Vercel CLI on self-hosted pool `SheikhGo-VM` |

**Not** this pipeline’s target: Railway (left intact).

Trigger branches: **main** and **master**.

Live API: `http://20.174.1.230:5082` · systemd unit: `sheikhgo-api` · health: `/health`

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

- Backend: `curl -s http://20.174.1.230:5082/health` → `Healthy`
- Logs: `journalctl -u sheikhgo-api -f` on the VM
- Frontend: Vercel dashboard deployment for ERP / customer-hub
