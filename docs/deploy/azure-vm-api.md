# SheikhGo API on Azure VM + Azure DevOps

## Live endpoints

| Item | Value |
|------|--------|
| Azure backend URL | `http://20.174.1.230:5082` |
| API base (clients) | `http://20.174.1.230:5082/api` |
| Health | `http://20.174.1.230:5082/health` |
| systemd unit | `sheikhgo-api` |
| Env file | `/etc/sheikhgo/api.env` (chmod 600) |
| Releases | `/opt/sheikhgo-api/releases/<stamp>` → `/opt/sheikhgo-api/current` |
| Swap | 2G `/swapfile` |
| SQL | local Docker `sheikhgo-sql` on `127.0.0.1:1433` (untouched) |
| Traccar | local `127.0.0.1:8082` (untouched) |
| Railway | **left fully intact** (still online; clients re-pointed to Azure) |

## Auto-deploy (primary): Azure DevOps self-hosted agent on the VM

Flow:

1. Push to GitHub `main` (paths under `Backend/**`, `deploy/azure-vm/**`, or `azure-pipelines.yml`)
2. Azure Pipelines picks up the GitHub push
3. Job runs on pool **`SheikhGo-VM`** (agent installed **on** `20.174.1.230`)
4. `deploy/azure-vm/deploy-api.sh` runs `dotnet publish` + `systemctl restart sheikhgo-api`

### What Ali must click once (Azure DevOps free org)

Azure CLI / existing org were not available from this workstation, so wire the org in the portal:

1. Create free org: https://dev.azure.com → **New organization** (Basic plan, ≤5 users).
2. Create project e.g. **`SheikhGo`**.
3. **Project settings → Agent pools → Add pool** named exactly **`SheikhGo-VM`** (self-hosted).
4. Create a PAT: User settings → Personal access tokens → **Agent Pools (Read & manage)** + **Code (Read)** if needed. Copy once; do not commit.
5. On the VM (SSH as `azureuser`):

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

6. **Pipelines → New pipeline → GitHub** → authorize `alirazatahir1234/Sheikh-Travel-System` → **Existing Azure Pipelines YAML** → `/azure-pipelines.yml`.
7. Run once (manual) or push a no-op under `Backend/` to verify.

### How to trigger / verify

- **Trigger:** `git push origin main` (with Backend changes), or **Run pipeline** in Azure DevOps.
- **Verify:** `curl -s http://20.174.1.230:5082/health` → `Healthy`
- **Logs:** `journalctl -u sheikhgo-api -f`
- **Agent:** Azure DevOps → Agent pools → SheikhGo-VM → agent online

### Manual deploy (no ADO yet)

```bash
ssh -i <SheikhGo-VM_key.pem> azureuser@20.174.1.230
/opt/sheikhgo-api/deploy/deploy-api.sh
# or from repo checkout:
DEPLOY_BRANCH=main /opt/sheikhgo-api/src/deploy/azure-vm/deploy-api.sh
```

## Fallback (not primary): Microsoft-hosted build + SSH

If the self-hosted agent is unavailable, add a second pipeline that:

1. Uses `vmImage: ubuntu-latest`
2. `dotnet publish`
3. Copies artifacts over SSH to `/opt/sheikhgo-api/releases/...` and restarts `sheikhgo-api`

Prefer the self-hosted path so build happens on the VM like Railway’s “push → build → run”.

## Caveats

- **HTTP only** (no TLS cert yet). Android release needs cleartext exception for `20.174.1.230`.
- **CORS** already allows sheikhgo.com / Vercel ERP hosts; local Angular uses proxy.
- **RAM:** ~4 GB VM with SQL + Traccar + API; 2 GB swap enabled. Keep API `MemoryMax=512M`.
- **Do not** restart/reconfigure Traccar or `sheikhgo-sql` for API deploys.
- **Do not** pause/delete Railway until you explicitly choose to.
