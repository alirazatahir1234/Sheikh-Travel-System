#!/usr/bin/env bash
# Phase B — production security smoke (go-live plan §B)
# Usage:
#   export SMOKE_EMAIL='your-erp-user@example.com'
#   export SMOKE_PASSWORD='...'
#   export SMOKE_PORTAL_PHONE='+923001234567'   # optional; skip portal if unset
#   ./docs/production-readiness/phase-b-security-smoke.sh
#
# Never commit real credentials. Do not paste tokens into chat logs.

set -euo pipefail

BASE_URL="${SMOKE_BASE_URL:-https://sheikh-travel-system-production.up.railway.app}"
EMAIL="${SMOKE_EMAIL:-}"
PASSWORD="${SMOKE_PASSWORD:-}"
PORTAL_PHONE="${SMOKE_PORTAL_PHONE:-}"
FOREIGN_TENANT_ID="${SMOKE_FOREIGN_TENANT_ID:-2}"
MATCH_TENANT_ID="${SMOKE_MATCH_TENANT_ID:-1}"

PASS=0
FAIL=0
SKIP=0

green() { printf '\033[32m%s\033[0m\n' "$*"; }
red()   { printf '\033[31m%s\033[0m\n' "$*"; }
yellow(){ printf '\033[33m%s\033[0m\n' "$*"; }

check() {
  local id="$1" title="$2" expected="$3" actual="$4"
  if [[ "$actual" == "$expected" ]]; then
    green "PASS  B$id  $title (HTTP $actual)"
    PASS=$((PASS + 1))
  else
    red "FAIL  B$id  $title (expected HTTP $expected, got $actual)"
    FAIL=$((FAIL + 1))
  fi
}

skip() {
  local id="$1" title="$2" reason="$3"
  yellow "SKIP  B$id  $title ($reason)"
  SKIP=$((SKIP + 1))
}

http_code() {
  # args: method url [curl extra...]
  local method="$1" url="$2"
  shift 2
  curl -sS --compressed -o /tmp/sheikhgo-smoke-body.json -w '%{http_code}' \
    -X "$method" "$url" \
    -H 'Accept: application/json' \
    "$@"
}

echo "Phase B security smoke"
echo "Base URL: $BASE_URL"
echo "----------------------------------------"

# --- B1.1 Login ---
if [[ -z "$EMAIL" || -z "$PASSWORD" ]]; then
  red "Set SMOKE_EMAIL and SMOKE_PASSWORD first."
  exit 2
fi

CODE=$(http_code POST "$BASE_URL/api/Auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")

# Keep login body separate — later requests overwrite the shared body file.
cp -f /tmp/sheikhgo-smoke-body.json /tmp/sheikhgo-smoke-login.json 2>/dev/null || true

# API returns HTTP 200 even for failed logins (ApiResponse.success=false, data=null).
LOGIN_OK=0
LOGIN_MSG=""
if [[ "$CODE" == "200" ]]; then
  python3 - <<'PY' > /tmp/sheikhgo-smoke-login-meta.txt
import json
try:
    with open("/tmp/sheikhgo-smoke-login.json", encoding="utf-8") as f:
        data = json.load(f)
except Exception as e:
    print("0")
    print(str(e)[:160])
    raise SystemExit(0)
ok = bool(data.get("success") or data.get("Success"))
msg = data.get("message") or data.get("Message") or ""
safe = "".join(c if c.isalnum() or c in " ._-+@" else " " for c in str(msg))[:160]
print("1" if ok else "0")
print(safe)
PY
  LOGIN_OK=$(sed -n '1p' /tmp/sheikhgo-smoke-login-meta.txt)
  LOGIN_MSG=$(sed -n '2p' /tmp/sheikhgo-smoke-login-meta.txt)
fi

if [[ "$CODE" == "200" && "$LOGIN_OK" == "1" ]]; then
  check 1 "Login with valid credentials" "200" "$CODE"
else
  red "FAIL  B1  Login failed (HTTP $CODE, success=$LOGIN_OK)"
  [[ -n "$LOGIN_MSG" ]] && yellow "      message: $LOGIN_MSG"
  yellow "      Check SMOKE_EMAIL / SMOKE_PASSWORD (typos like .comm vs .com)."
  FAIL=$((FAIL + 1))
fi

TOKEN=""
if [[ "$LOGIN_OK" == "1" ]]; then
  TOKEN=$(python3 - <<'PY'
import json, sys
try:
    with open("/tmp/sheikhgo-smoke-login.json", encoding="utf-8") as f:
        data = json.load(f)
except Exception:
    sys.exit(0)

def walk(obj):
    if isinstance(obj, dict):
        for k, v in obj.items():
            lk = k.lower()
            if lk in ("accesstoken", "access_token", "token") and isinstance(v, str) and len(v) > 20:
                print(v)
                return True
            if walk(v):
                return True
    elif isinstance(obj, list):
        for v in obj:
            if walk(v):
                return True
    return False

walk(data)
PY
)
  if [[ -n "${TOKEN}" ]]; then
    green "      token extracted (length ${#TOKEN}; value not printed)"
  else
    red "      success=true but no accessToken in body"
    FAIL=$((FAIL + 1))
  fi
fi

TOKEN="${SMOKE_TOKEN:-$TOKEN}"
if [[ -n "$TOKEN" ]]; then
  AUTH_H=(-H "Authorization: Bearer $TOKEN")
else
  AUTH_H=()
fi

# Platform operator (SUPER_ADMIN) may override X-Tenant-Id — B5 expects 200 then.
IS_PLATFORM_OPS=0
if [[ -n "$TOKEN" ]]; then
  IS_PLATFORM_OPS=$(python3 - <<'PY'
import json, base64, sys
# 1) Prefer login body roles
try:
    with open("/tmp/sheikhgo-smoke-login.json", encoding="utf-8") as f:
        data = json.load(f)
    d = data.get("data") or data.get("Data") or {}
    roles = []
    for key in ("roles", "Roles", "role", "Role"):
        v = d.get(key)
        if isinstance(v, list):
            roles.extend(str(x) for x in v)
        elif isinstance(v, str) and v:
            roles.append(v)
    if any(r.upper().replace("-", "_") in ("SUPER_ADMIN", "SUPERADMIN") for r in roles):
        print("1")
        raise SystemExit(0)
except SystemExit:
    raise
except Exception:
    pass
# 2) Decode JWT payload (no verify) for role claims
try:
    with open("/tmp/sheikhgo-smoke-login.json", encoding="utf-8") as f:
        data = json.load(f)
    # token may only be in env via shell; read from argv file written below — skip
except Exception:
    pass
print("0")
PY
)
  # Also inspect JWT roles from TOKEN itself
  if [[ "$IS_PLATFORM_OPS" != "1" ]]; then
    IS_PLATFORM_OPS=$(TOKEN="$TOKEN" python3 - <<'PY'
import json, base64, os
tok = os.environ.get("TOKEN", "")
parts = tok.split(".")
if len(parts) < 2:
    print("0"); raise SystemExit(0)
pad = "=" * (-len(parts[1]) % 4)
try:
    payload = json.loads(base64.urlsafe_b64decode(parts[1] + pad))
except Exception:
    print("0"); raise SystemExit(0)
vals = []
for k in ("role", "roles", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"):
    v = payload.get(k)
    if isinstance(v, list):
        vals.extend(str(x) for x in v)
    elif v is not None:
        vals.append(str(v))
if any(r.upper().replace("-", "_") in ("SUPER_ADMIN", "SUPERADMIN") for r in vals):
    print("1")
else:
    print("0")
PY
)
  fi
  if [[ "$IS_PLATFORM_OPS" == "1" ]]; then
    yellow "      login user is SUPER_ADMIN (platform operator) — B5 expects override 200, not 403"
  fi
fi

# --- B1.2 Protected without token ---
CODE=$(http_code GET "$BASE_URL/api/Auth/me")
check 2 "Protected /api/Auth/me without token" "401" "$CODE"

# --- B1.3 Invalid token ---
CODE=$(http_code GET "$BASE_URL/api/Auth/me" -H 'Authorization: Bearer invalid.token.value')
check 3 "Protected endpoint with invalid token" "401" "$CODE"

if [[ -z "$TOKEN" ]]; then
  red "No token — skipping B4–B6 tenant checks."
  FAIL=$((FAIL + 1))
else
  # --- B2.4 Dashboard without X-Tenant-Id ---
  CODE=$(http_code GET "$BASE_URL/api/Dashboard/summary" "${AUTH_H[@]}")
  check 4 "Dashboard summary without X-Tenant-Id (JWT tenant)" "200" "$CODE"

  # --- B2.5 Mismatched X-Tenant-Id ---
  CODE=$(http_code GET "$BASE_URL/api/Dashboard/summary" \
    "${AUTH_H[@]}" -H "X-Tenant-Id: $FOREIGN_TENANT_ID")
  if [[ "$IS_PLATFORM_OPS" == "1" ]]; then
    check 5 "Dashboard foreign X-Tenant-Id as SUPER_ADMIN (override allowed)" "200" "$CODE"
  else
    check 5 "Dashboard with foreign X-Tenant-Id ($FOREIGN_TENANT_ID)" "403" "$CODE"
  fi

  # --- B2.6 Matching X-Tenant-Id ---
  CODE=$(http_code GET "$BASE_URL/api/Dashboard/summary" \
    "${AUTH_H[@]}" -H "X-Tenant-Id: $MATCH_TENANT_ID")
  check 6 "Dashboard with matching X-Tenant-Id ($MATCH_TENANT_ID)" "200" "$CODE"
fi

# --- B3.7 Dev endpoint anonymous ---
CODE=$(http_code POST "$BASE_URL/api/dev/seed" -H 'Content-Type: application/json' -d '{}')
# Expect 401 (Authorize) or 404 (non-Development defense-in-depth)
if [[ "$CODE" == "401" || "$CODE" == "404" ]]; then
  green "PASS  B7  /api/dev/seed anonymous (HTTP $CODE — 401 or 404 OK)"
  PASS=$((PASS + 1))
else
  red "FAIL  B7  /api/dev/seed anonymous (expected 401 or 404, got $CODE)"
  FAIL=$((FAIL + 1))
fi

# --- B3.8 Lookup public ---
CODE=$(http_code GET "$BASE_URL/api/lookup/timezones")
check 8 "Public lookup timezones" "200" "$CODE"

# --- B4.9 Portal OTP ---
if [[ -z "$PORTAL_PHONE" ]]; then
  skip 9 "Portal send-otp" "set SMOKE_PORTAL_PHONE to run"
else
  CODE=$(http_code POST "$BASE_URL/api/customer-portal/auth/send-otp" \
    -H 'Content-Type: application/json' \
    -d "{\"phone\":\"$PORTAL_PHONE\"}")
  check 9 "Portal send-otp" "200" "$CODE"
  if [[ "$CODE" == "200" ]] && command -v jq >/dev/null 2>&1; then
    DEVMODE=$(jq -r '.data.devMode // .data.DevMode // false' /tmp/sheikhgo-smoke-body.json 2>/dev/null || echo false)
    MSG=$(jq -r '.data.message // .data.Message // empty' /tmp/sheikhgo-smoke-body.json 2>/dev/null || true)
    if [[ "$DEVMODE" == "true" ]]; then
      red "FAIL  B10 Portal DevMode=true on production (bypass must be off)"
      FAIL=$((FAIL + 1))
    else
      green "PASS  B10 Portal DevMode=false (SMS / non-dev path)"
      PASS=$((PASS + 1))
    fi
    [[ -n "$MSG" ]] && echo "      message: $MSG"
  else
    skip 10 "Portal DevMode flag" "jq missing or non-200"
  fi
fi

echo "----------------------------------------"
echo "Results: PASS=$PASS FAIL=$FAIL SKIP=$SKIP"
if [[ "$FAIL" -gt 0 ]]; then
  red "Phase B FAILED — fix P0 issues before Phase C."
  exit 1
fi
green "Phase B PASSED — proceed to Phase C critical E2E dry-run."
exit 0
