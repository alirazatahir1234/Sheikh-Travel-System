#!/usr/bin/env bash
# Deploy SheikhGo Angular frontends to Vercel (production).
# Requires: VERCEL_TOKEN, VERCEL_ORG_ID, VERCEL_ERP_PROJECT_ID
# Optional: VERCEL_HUB_PROJECT_ID — skipped when unset or PLACEHOLDER so ERP (shego.com) still deploys.
set -euo pipefail

ROOT="${BUILD_SOURCESDIRECTORY:-$(cd "$(dirname "$0")/../.." && pwd)}"

: "${VERCEL_TOKEN:?VERCEL_TOKEN is required (ADO secret)}"
: "${VERCEL_ORG_ID:?VERCEL_ORG_ID is required}"
: "${VERCEL_ERP_PROJECT_ID:?VERCEL_ERP_PROJECT_ID is required (sheikhgo-erp)}"

is_placeholder_id() {
  local id="${1:-}"
  [[ -z "$id" ]] && return 0
  [[ "$id" == *[Pp][Ll][Aa][Cc][Ee][Hh][Oo][Ll][Dd][Ee][Rr]* ]] && return 0
  [[ "$id" == "TODO" || "$id" == "todo" || "$id" == "changeme" ]] && return 0
  return 1
}

PROD_FLAG=()
if [[ "${VERCEL_PROD:-true}" == "true" ]]; then
  PROD_FLAG=(--prod)
fi

npm install -g vercel@latest

deploy_one() {
  local name="$1"
  local dir="$2"
  local project_id="$3"

  if is_placeholder_id "$project_id"; then
    echo "=== Skipping ${name}: Vercel project id is missing or PLACEHOLDER ==="
    return 0
  fi

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
deploy_one "sheikh-travel-customer-hub" "$ROOT/Frontend/sheikh-travel-customer-hub" "${VERCEL_HUB_PROJECT_ID:-}"

echo "=== Vercel deploys finished ==="
