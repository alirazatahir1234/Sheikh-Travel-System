#!/usr/bin/env bash
# Deploy SheikhGo Angular frontends to Vercel (production).
# Requires: VERCEL_TOKEN, VERCEL_ORG_ID, VERCEL_ERP_PROJECT_ID
# Optional: VERCEL_HUB_PROJECT_ID — skipped when unset or PLACEHOLDER so ERP still deploys.
#
# Path model (critical):
#   Vercel project Root Directory = Frontend/<app>
#   Always run vercel from the REPO ROOT so that path resolves once.
#   Never cd into Frontend/... first (doubles to …/Frontend/.../Frontend/... and
#   yields empty ~38ms builds).
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

# Ensure remote rootDirectory matches app path relative to repo root.
ensure_root_directory() {
  local project_id="$1"
  local app_rel="$2"
  echo "Ensuring Vercel rootDirectory=${app_rel} for ${project_id}..."
  curl -sS -X PATCH "https://api.vercel.com/v9/projects/${project_id}?teamId=${VERCEL_ORG_ID}" \
    -H "Authorization: Bearer ${VERCEL_TOKEN}" \
    -H "Content-Type: application/json" \
    -d "{\"rootDirectory\":\"${app_rel}\"}" \
    >/tmp/vercel-patch-root.json || true
  python3 - <<'PY' || true
import json
try:
    d=json.load(open("/tmp/vercel-patch-root.json"))
    print("remote rootDirectory:", repr(d.get("rootDirectory")))
except Exception as e:
    print("patch parse:", e)
PY
}

# Vercel Hobby rejects any single file >100MB (hero-demo.mp4 historically 103MB).
assert_no_oversized_files() {
  local bad
  bad="$(find .vercel/output -type f -size +90M 2>/dev/null || true)"
  if [[ -n "$bad" ]]; then
    echo "ERROR: files over ~90MB will fail Vercel deploy (100MB limit):"
    echo "$bad"
    exit 1
  fi
}

deploy_one() {
  local name="$1"
  local app_rel="$2"
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

  echo "=== Deploying ${name} → Vercel project ${project_id} (repo root + rootDirectory=${app_rel}) ==="
  if [[ -f "${dir}/vercel.json" ]]; then
    echo "App vercel.json:"
    cat "${dir}/vercel.json"
  fi

  ensure_root_directory "$project_id" "$app_rel"

  mkdir -p "${ROOT}/.vercel"
  cat > "${ROOT}/.vercel/project.json" <<EOF
{"orgId":"${VERCEL_ORG_ID}","projectId":"${project_id}"}
EOF

  (
    cd "$ROOT"
    export VERCEL_PROJECT_ID="$project_id"
    # Drop leftover app-subdir links so CLI uses repo-root + rootDirectory.
    rm -rf Frontend/sheikhgo-erp/.vercel/project.json Frontend/sheikh-travel-customer-hub/.vercel/project.json 2>/dev/null || true
    vercel pull --yes --environment=production --token "$VERCEL_TOKEN"
    vercel build "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"

    if [[ ! -d .vercel/output/static ]] && [[ ! -d .vercel/output/functions ]]; then
      echo "ERROR: .vercel/output has no static/ or functions/ — empty/fake build."
      ls -la .vercel/output 2>/dev/null || true
      exit 1
    fi
    assert_no_oversized_files
    echo "Build output OK:"
    du -sh .vercel/output/static 2>/dev/null || true

    vercel deploy --prebuilt "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"
  )
}

deploy_one "sheikhgo-erp" "Frontend/sheikhgo-erp" "$VERCEL_ERP_PROJECT_ID"
deploy_one "sheikh-travel-customer-hub" "Frontend/sheikh-travel-customer-hub" "${VERCEL_HUB_PROJECT_ID:-}"

echo "=== Vercel deploys finished ==="
