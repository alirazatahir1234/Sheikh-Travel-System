#!/usr/bin/env bash
# Deploy SheikhGo Angular frontends to Vercel (production).
# Requires: VERCEL_TOKEN, VERCEL_ORG_ID, VERCEL_ERP_PROJECT_ID, VERCEL_HUB_PROJECT_ID
set -euo pipefail

ROOT="${BUILD_SOURCESDIRECTORY:-$(cd "$(dirname "$0")/../.." && pwd)}"

: "${VERCEL_TOKEN:?VERCEL_TOKEN is required (ADO secret)}"
: "${VERCEL_ORG_ID:?VERCEL_ORG_ID is required}"
: "${VERCEL_ERP_PROJECT_ID:?VERCEL_ERP_PROJECT_ID is required (sheikhgo-erp)}"
: "${VERCEL_HUB_PROJECT_ID:?VERCEL_HUB_PROJECT_ID is required (sheikh-travel-customer-hub)}"

PROD_FLAG=()
if [[ "${VERCEL_PROD:-true}" == "true" ]]; then
  PROD_FLAG=(--prod)
fi

npm install -g vercel@latest

deploy_one() {
  local name="$1"
  local dir="$2"
  local project_id="$3"

  echo "=== Deploying ${name} → Vercel project ${project_id} ==="
  mkdir -p "${dir}/.vercel"
  cat > "${dir}/.vercel/project.json" <<EOF
{"orgId":"${VERCEL_ORG_ID}","projectId":"${project_id}"}
EOF

  (
    cd "$dir"
    vercel pull --yes --environment=production --token "$VERCEL_TOKEN"
    vercel build "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"
    vercel deploy --prebuilt "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"
  )
}

deploy_one "sheikhgo-erp" "$ROOT/Frontend/sheikhgo-erp" "$VERCEL_ERP_PROJECT_ID"
deploy_one "sheikh-travel-customer-hub" "$ROOT/Frontend/sheikh-travel-customer-hub" "$VERCEL_HUB_PROJECT_ID"

echo "=== Vercel deploys finished ==="
