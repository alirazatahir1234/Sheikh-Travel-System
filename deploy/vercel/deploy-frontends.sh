#!/usr/bin/env bash
# Deploy SheikhGo Angular frontends to Vercel (production).
# Requires: VERCEL_TOKEN, VERCEL_ORG_ID, VERCEL_ERP_PROJECT_ID
# Optional: VERCEL_HUB_PROJECT_ID — skipped when unset or PLACEHOLDER so ERP still deploys.
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

# Dashboard Root Directory (e.g. Frontend/sheikhgo-erp) doubles when CLI cwd is already that folder.
# Clear it after pull so build/deploy use the app directory + local vercel.json.
clear_root_directory() {
  local project_json="$1"
  python3 - "$project_json" <<'PY'
import json, sys
path = sys.argv[1]
try:
    with open(path, encoding="utf-8") as f:
        data = json.load(f)
except FileNotFoundError:
    sys.exit(0)
changed = False
settings = data.get("settings")
if isinstance(settings, dict):
    for key in ("rootDirectory", "root_directory"):
        if key in settings:
            settings.pop(key, None)
            changed = True
if changed:
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    print(f"Cleared Vercel rootDirectory in {path}")
PY
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

  # When VERCEL_ORG_ID is in the environment (ADO Library), the CLI also requires
  # VERCEL_PROJECT_ID — project.json alone is not enough.
  (
    cd "$dir"
    export VERCEL_PROJECT_ID="$project_id"
    vercel pull --yes --environment=production --token "$VERCEL_TOKEN"
    clear_root_directory "${dir}/.vercel/project.json"
    vercel build "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"
    vercel deploy --prebuilt "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"
  )
}

deploy_one "sheikhgo-erp" "$ROOT/Frontend/sheikhgo-erp" "$VERCEL_ERP_PROJECT_ID"
deploy_one "sheikh-travel-customer-hub" "$ROOT/Frontend/sheikh-travel-customer-hub" "${VERCEL_HUB_PROJECT_ID:-}"

echo "=== Vercel deploys finished ==="
