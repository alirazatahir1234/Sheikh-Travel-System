# SheikhGo CI/CD — Backend → IBM · Frontend → Vercel

Org **Alirazatahir** · Project **SheikhGo** · Pipeline file: `azure-pipelines.yml`

| Target | Trigger paths | How |
|--------|---------------|-----|
| **IBM** (API) | `Backend/**`, `deploy/ibm/**` | `deploy/ibm/deploy-api.sh` after `dotnet publish` |
| **Vercel** (ERP + Customer Hub) | `Frontend/sheikhgo-erp/**`, `Frontend/sheikh-travel-customer-hub/**` | `deploy/vercel/deploy-frontends.sh` via Vercel CLI |

**Not** this pipeline’s target: Azure VM `20.174.1.230` / `sheikhgo-api` (leave running). Railway left intact.

Trigger branches: **main** and **master**.

---

## What’s missing for IBM (blocking real deploy)

Workspace search found **no** IBM host, SSH key, Code Engine project, Cloud Foundry org/space, API key, or Dockerfile tailored to IBM.

Ali must provide one deploy method and fill ADO variables:

### Option A — SSH to IBM VPC / VM (simplest if a Linux host already runs the API)

| Variable | Example |
|----------|---------|
| `IBM_DEPLOY_METHOD` | `ssh` |
| `IBM_HOST` | public IP or DNS |
| `IBM_SSH_USER` | e.g. `ubuntu` |
| `IBM_SSH_KEY` | private key (ADO **secret**) |
| `IBM_REMOTE_PATH` | e.g. `/opt/sheikhgo-api/current` |
| `IBM_SERVICE_NAME` | e.g. `sheikhgo-api` |

### Option B — IBM Code Engine

| Variable | Notes |
|----------|-------|
| `IBM_DEPLOY_METHOD` | `codeengine` |
| `IBM_API_KEY` | IAM API key (secret) |
| `IBM_REGION` | e.g. `us-south` |
| `IBM_RESOURCE_GROUP` | resource group name |
| `IBM_CE_PROJECT` | Code Engine project |
| `IBM_APP_NAME` | application name |
| Plus | container registry + image build (script currently exits until Ali confirms image flow) |

### Option C — IBM Cloud Foundry

| Variable | Notes |
|----------|-------|
| `IBM_DEPLOY_METHOD` | `cloudfoundry` |
| `IBM_API_KEY`, `IBM_REGION`, `IBM_CF_ORG`, `IBM_CF_SPACE`, `IBM_APP_NAME` | |
| Plus | `manifest.yml` / buildpack (script exits until provided) |

Until those are set, the **Backend → IBM** job will fail with a clear error (by design).

---

## Vercel (ready once tokens exist)

Existing `vercel.json` in:

- `Frontend/sheikhgo-erp/`
- `Frontend/sheikh-travel-customer-hub/`

Known public hosts (from API CORS): `sheikhgo-erp.vercel.app`, `sheikh-travel-customer-hub.vercel.app`.

| Variable | Where to get it |
|----------|-----------------|
| `VERCEL_TOKEN` | Vercel → Settings → Tokens |
| `VERCEL_ORG_ID` | Project → Settings → General |
| `VERCEL_ERP_PROJECT_ID` | sheikhgo-erp project settings |
| `VERCEL_HUB_PROJECT_ID` | customer-hub project settings |
| `VERCEL_PROD` | `true` (default) |

No local `.vercel/project.json` is committed (IDs come from ADO at deploy time).

---

## Create / wire the pipeline in Azure DevOps (after Ali signs in)

1. Open https://dev.azure.com/Alirazatahir/SheikhGo
2. **Pipelines → New pipeline → Azure Repos Git → SheikhGo**
3. Select **Existing Azure Pipelines YAML file** → `/azure-pipelines.yml`
4. **Pipelines → Library** (or pipeline **Variables**): add the IBM_* and VERCEL_* variables above; mark secrets
5. Save & enable; optionally **Run pipeline** on `main`

Azure VM agent pool `SheikhGo-VM` is **not** required for this IBM/Vercel pipeline (uses Microsoft-hosted `ubuntu-latest`).
