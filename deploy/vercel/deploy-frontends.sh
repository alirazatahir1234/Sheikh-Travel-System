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

# Angular/Node OOM guard (exit 137 / SIGKILL on SheikhGo-VM ~4Gi RAM).
# Cap heap below full RAM; rely on swap for peak. Do not set to full physical RAM.
export NODE_OPTIONS="${NODE_OPTIONS:---max-old-space-size=3072}"
# Limit Angular esbuild/webpack parallel workers on the small self-hosted agent.
export NG_BUILD_MAX_WORKERS="${NG_BUILD_MAX_WORKERS:-1}"
export NODE_ENV="${NODE_ENV:-production}"

echo "=== Agent memory before frontend build ==="
free -h || true
swapon --show || true
echo "NODE_OPTIONS=${NODE_OPTIONS}"
echo "NG_BUILD_MAX_WORKERS=${NG_BUILD_MAX_WORKERS}"

# Browser Maps key — required for ERP live map. Prefer ADO/Vercel secret; local secrets.local is fallback inside inject-maps-env.mjs.
if [[ -z "${GOOGLE_MAPS_API_KEY:-}" ]]; then
  echo "WARN: GOOGLE_MAPS_API_KEY unset — ERP build will use environment.secrets.local.ts if present, else fail."
fi

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
    # Ensure Maps inject sees ADO/Vercel secret during `vercel build` → npm run build:prod.
    # Note: `vercel build` off-Vercel does NOT inherit shell env ("System environment variables
    # will not be available"). We must write the key into .vercel/.env.production.local and/or
    # pre-run inject-maps-env.mjs so the Angular prod bundle actually bakes AIzaSy…
    if [[ -n "${GOOGLE_MAPS_API_KEY:-}" ]]; then
      export GOOGLE_MAPS_API_KEY
    fi
    if [[ -n "${GOOGLE_MAPS_MAP_ID:-}" ]]; then
      export GOOGLE_MAPS_MAP_ID
    fi
    # Drop leftover app-subdir links so CLI uses repo-root + rootDirectory.
    rm -rf Frontend/sheikhgo-erp/.vercel/project.json Frontend/sheikh-travel-customer-hub/.vercel/project.json 2>/dev/null || true
    vercel pull --yes --environment=production --token "$VERCEL_TOKEN"

    # After pull, force ADO/shell Maps secrets into the env file vercel build loads.
    ENV_LOCAL="${ROOT}/.vercel/.env.production.local"
    mkdir -p "${ROOT}/.vercel"
    touch "$ENV_LOCAL"
    if [[ -n "${GOOGLE_MAPS_API_KEY:-}" ]]; then
      grep -vE '^GOOGLE_MAPS_API_KEY=' "$ENV_LOCAL" > "${ENV_LOCAL}.tmp" || true
      mv "${ENV_LOCAL}.tmp" "$ENV_LOCAL"
      printf 'GOOGLE_MAPS_API_KEY=%s\n' "$GOOGLE_MAPS_API_KEY" >> "$ENV_LOCAL"
      echo "Wrote GOOGLE_MAPS_API_KEY into .vercel/.env.production.local (len=${#GOOGLE_MAPS_API_KEY})"
    fi
    if [[ -n "${GOOGLE_MAPS_MAP_ID:-}" ]]; then
      grep -vE '^GOOGLE_MAPS_MAP_ID=' "$ENV_LOCAL" > "${ENV_LOCAL}.tmp" || true
      mv "${ENV_LOCAL}.tmp" "$ENV_LOCAL"
      printf 'GOOGLE_MAPS_MAP_ID=%s\n' "$GOOGLE_MAPS_MAP_ID" >> "$ENV_LOCAL"
    fi
    # Patch environment.prod.ts before vercel/ng build (ERP). Prefer inject-maps-env.mjs when
    # present; otherwise sed-replace googleMapsApiKey (Azure repo may lack the inject script).
    if [[ "$name" == "sheikhgo-erp" && -n "${GOOGLE_MAPS_API_KEY:-}" ]]; then
      if [[ -f Frontend/sheikhgo-erp/scripts/inject-maps-env.mjs ]]; then
        (cd Frontend/sheikhgo-erp && node scripts/inject-maps-env.mjs)
      else
        ENV_PROD_TS="${ROOT}/Frontend/sheikhgo-erp/src/environments/environment.prod.ts"
        if ! grep -qE 'googleMapsApiKey[[:space:]]*:' "$ENV_PROD_TS"; then
          echo "ERROR: environment.prod.ts missing googleMapsApiKey" >&2
          exit 1
        fi
        # Keys are AIzaSy + [A-Za-z0-9_-] — safe for sed delimiter |
        sed -i -E "s|googleMapsApiKey:[[:space:]]*'[^']*'|googleMapsApiKey: '${GOOGLE_MAPS_API_KEY}'|" "$ENV_PROD_TS"
        if [[ -n "${GOOGLE_MAPS_MAP_ID:-}" ]]; then
          if grep -qE 'googleMapsMapId[[:space:]]*:' "$ENV_PROD_TS"; then
            sed -i -E "s|googleMapsMapId:[[:space:]]*'[^']*'|googleMapsMapId: '${GOOGLE_MAPS_MAP_ID}'|" "$ENV_PROD_TS"
          fi
        fi
        if ! grep -qE "googleMapsApiKey:[[:space:]]*'AIzaSy" "$ENV_PROD_TS"; then
          echo "ERROR: sed failed to bake GOOGLE_MAPS_API_KEY into environment.prod.ts" >&2
          exit 1
        fi
        echo "Sed-patched environment.prod.ts googleMapsApiKey (len=${#GOOGLE_MAPS_API_KEY})"
      fi
    fi

    vercel build "${PROD_FLAG[@]}" --token "$VERCEL_TOKEN"

    if [[ ! -d .vercel/output/static ]] && [[ ! -d .vercel/output/functions ]]; then
      echo "ERROR: .vercel/output has no static/ or functions/ — empty/fake build."
      ls -la .vercel/output 2>/dev/null || true
      exit 1
    fi

    # Safety net: never ship Mixed Content HTTP API URL.
    # Prefer Cloudflare quick tunnel from environment.prod.ts when present;
    # otherwise fall back to stable Caddy/sslip HTTPS (not ephemeral).
    if [[ -d .vercel/output/static ]]; then
      ENV_PROD="${ROOT}/Frontend/sheikhgo-erp/src/environments/environment.prod.ts"
      if [[ "$name" == "sheikhgo-erp" ]] && grep -qE "apiUrl:[[:space:]]*'http://20\\.174\\.1\\.230:5082" "$ENV_PROD" 2>/dev/null; then
        echo "WARN: environment.prod.ts still has raw HTTP apiUrl — will rewrite build output to HTTPS." >&2
      fi
      TUNNEL_HOST="$(grep -oE 'https://[a-zA-Z0-9.-]+\.trycloudflare\.com' \
        "$ENV_PROD" 2>/dev/null | head -1 || true)"
      REWRITE_TO="${TUNNEL_HOST:-https://20-174-1-230.sslip.io}"
      if grep -Rql 'http://20\.174\.1\.230:5082' .vercel/output/static --include='*.js' 2>/dev/null; then
        echo "Rewriting baked http://20.174.1.230:5082 → ${REWRITE_TO} in build output"
        find .vercel/output/static -type f -name '*.js' -print0 \
          | xargs -0 grep -l 'http://20\.174\.1\.230:5082' \
          | while read -r f; do
              sed -i "s|http://20\\.174\\.1\\.230:5082|${REWRITE_TO}|g" "$f"
              echo "  patched $f"
            done
      fi
      if grep -Rql 'http://20\.174\.1\.230:5082' .vercel/output/static --include='*.js' 2>/dev/null; then
        echo "ERROR: HTTP API URL still present after rewrite — refusing deploy." >&2
        exit 1
      fi
      # Refuse any remaining non-HTTPS API host for ERP (defense in depth).
      if [[ "$name" == "sheikhgo-erp" ]] && grep -RqlE 'http://20\.174\.1\.230(:[0-9]+)?' .vercel/output/static --include='*.js' 2>/dev/null; then
        echo "ERROR: Non-HTTPS Azure VM API host still present in ERP build — refusing deploy." >&2
        exit 1
      fi
      # ERP must not ship an empty Maps key (live map init fails).
      if [[ "$name" == "sheikhgo-erp" ]]; then
        if ! grep -Rql 'AIzaSy' .vercel/output/static --include='*.js' 2>/dev/null; then
          echo "ERROR: No Google Maps API key baked into ERP build — set GOOGLE_MAPS_API_KEY (ADO/Vercel) or secrets.local." >&2
          exit 1
        fi
      fi
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
