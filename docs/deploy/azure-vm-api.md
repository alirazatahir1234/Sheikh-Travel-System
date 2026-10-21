# SheikhGo API on Azure VM + Azure DevOps

Primary CI/CD: **Backend → Azure VM** and **Frontend → Vercel** — see [azure-vm-vercel-cicd.md](./azure-vm-vercel-cicd.md). Pipeline file: root `azure-pipelines.yml`.

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
- **Verify:** `curl -s http://20.174.1.230:5082/health` → `Healthy`
- **Logs:** `journalctl -u sheikhgo-api -f`
- **Agent:** Azure DevOps → Agent pools → SheikhGo-VM → agent online

### Manual deploy (no ADO)

```bash
ssh azureuser@20.174.1.230
/opt/sheikhgo-api/deploy/deploy-api.sh
# or from repo checkout:
DEPLOY_BRANCH=main /opt/sheikhgo-api/src/deploy/azure-vm/deploy-api.sh
```

## Caveats

- **HTTP only** (no TLS cert yet). Android release needs cleartext exception for `20.174.1.230`.
- **CORS** already allows sheikhgo.com / Vercel ERP hosts; local Angular uses proxy.
- **RAM:** ~4 GB VM with SQL + Traccar + API; 2 GB swap enabled. Keep API `MemoryMax=512M`.
- **Do not** restart/reconfigure Traccar or `sheikhgo-sql` for API deploys.
- **Do not** pause/delete Railway until you explicitly choose to.
