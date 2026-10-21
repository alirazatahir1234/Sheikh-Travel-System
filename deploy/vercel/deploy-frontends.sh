#!/usr/bin/env bash
# Deploy SheikhGo Angular frontends to Vercel (production).
# Requires: VERCEL_TOKEN, VERCEL_ORG_ID, VERCEL_ERP_PROJECT_ID
# Optional: VERCEL_HUB_PROJECT_ID — skipped when unset or PLACEHOLDER so ERP still deploys.
#
# IMPORTANT — path model:
#   Vercel project "sheikh-travel-system" Root Directory = Frontend/sheikhgo-erp
#   We ALWAYS run vercel from the REPO ROOT and set rootDirectory to the app
#   relative path once. Never cd into the app first (that doubles the path to
#   …/Frontend/sheikhgo-erp/Frontend/sheikhgo-erp and yields empty 38ms builds).
set -euo pipefail

ROOT="${BUILD_SOURCESDIRECTORY:-$(cd "$(dirname "$0")/../.." && pwd)}"

: "${VERCEL_TOKEN:?VERCEL_TOKEN is required (ADO secret)}"
: "${VERCEL_ORG_ID:?VERCEL_ORG_ID is required}"
: "${VERCEL_ERP_PROJECT_ID:?VERCEL_ERP_PROJECT_ID is required (sheikh-travel-system)}"

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

# Ensure remote + local rootDirectory matches the app path relative to repo root.
sync_root_directory() {
  local project_id="$1"
  local app_rel="$2" # e.g. Frontend/sheikhgo-erp
  local team_qs="?teamId=${VERCEL_ORG_ID}"

  echo "Setting Vercel rootDirectory=${app_rel} for ${project_id}..."
  curl -sS -X PATCH "https://api.vercel.com/v9/projects/${project_id}${team_qs}" \
    -H "Authorization: Bearer ${VERCEL_TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{\"rootDirectory\":\"${app_rel}\"}" \
    >/tmp/vercel-patch-root.json || true
  python3 - <<'PY' || true
import json
try:
    with open("/tmp/vercel-patch-root.json") as f:
        d = json.load(f)
    print("remote rootDirectory:", repr(d.get("rootDirectory")))
    if d.get("error"):
        print("patch error:", d.get("error"))
except Exception as e:
    print("patch parse:", e)
PY

  local project_json=".vercel/project.json"
  if [[ -f "$project_json" ]]; then
    python3 - "$project_json" "$app_rel" <<'PY'
import json, sys
path, app_rel = sys.argv[1], sys.argv[2]
with open(path, encoding="utf-8") as f:
    data = json.load(f)
settings = data.setdefault("settings", {})
if not isinstance(settings, dict):
    settings = {}
    data["settings"] = settings
settings["rootDirectory"] = app_rel
# Prefer local vercel.json build settings when present under the app
with open(path, "w", encoding="utf-8") as f:
    json.dump(data, f, indent=2)
    f.write("\n")
print(f"Local settings.rootDirectory -> {app_rel}")
PY
  fi
}

deploy_one() {
  local name="$1"
  local app_rel="$2"   # relative to ROOT
  local project_id="$3"
  local dir="${ROOT}/${app_rel}"

  if is_placeholder_id "$project_id"; then
    echo "=== Skipping ${name}: Vercel project id is missing or PLACEHOLDER ==="
    return 0
  fi

  if [[ ! -d "$dir" ]]; then
    echo "=== ERROR: app directory missing: ${dir} ==="
    return 1
  fi
  if [[ ! -f "${dir}/vercel.json" ]]; then
    echo "=== ERROR: missing ${dir}/vercel.json (need buildCommand + outputDirectory) ==="
    return 1
  fi

  echo "=== Deploying ${name} → Vercel project ${project_id} (root=${ROOT}, rootDirectory=${app_rel}) ==="
  echo "App vercel.json:"
  cat "${dir}/vercel.json"

  mkdir -p "${ROOT}/.vercel"
  cat > "${ROOT}/.vercel/project.json" <<EOF
{"orgId":"${VERCEL_ORG_ID}","projectId":"${project_id}"}
EOF

  (
    cd "$ROOT"
    export VERCEL_PROJECT_ID="$project_id"
    vercel pull --yes --environment=production --token "$VERCEL_TOKEN"
    sync_root_directory "$project_id" "$app_rel"
    vercel build "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"

    if [[ ! -d .vercel/output/static ]] && [[ ! -d .vercel/output/functions ]]; then
      echo "ERROR: .vercel/output has no static/ or functions/ — Angular build did not produce deployable output."
      ls -laR .vercel/output 2>/dev/null || true
      exit 1
    fi
    echo "Build output OK:"
    du -sh .vercel/output/static 2>/dev/null || true
    ls .vercel/output/static | head -20

    vercel deploy --prebuilt "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"
  )
}

deploy_one "sheikhgo-erp" "Frontend/sheikhgo-erp" "$VERCEL_ERP_PROJECT_ID"
deploy_one "sheikh-travel-customer-hub" "Frontend/sheikh-travel-customer-hub" "${VERCEL_HUB_PROJECT_ID:-}"

echo "=== Vercel deploys finished ==="
