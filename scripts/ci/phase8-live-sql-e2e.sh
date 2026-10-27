#!/usr/bin/env bash
# Phase 8 — AI/MCP tests + live SQL MCP stdio E2E for CI.
# Never echoes connection strings or passwords.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
MCP_ROOT="${SHEIKHGO_MCP_ROOT:-}"
if [[ -z "$MCP_ROOT" ]]; then
  if [[ -f "$ROOT/../SheikhGo-MCP/dist/index.js" || -f "$ROOT/../SheikhGo-MCP/package.json" ]]; then
    MCP_ROOT="$(cd "$ROOT/../SheikhGo-MCP" && pwd)"
  elif [[ -f "$ROOT/SheikhGo-MCP/dist/index.js" || -f "$ROOT/SheikhGo-MCP/package.json" ]]; then
    MCP_ROOT="$(cd "$ROOT/SheikhGo-MCP" && pwd)"
  fi
fi

SQL_PASSWORD="${SHEIKHGO_PHASE8_SQL_PASSWORD:-${MSSQL_SA_PASSWORD:-}}"
SQL_HOST="${SHEIKHGO_PHASE8_SQL_HOST:-127.0.0.1,1433}"
SQL_USER="${SHEIKHGO_PHASE8_SQL_USER:-sa}"

echo "== Phase 8 AI/MCP CI =="
echo "Repo: $ROOT"
echo "MCP root: ${MCP_ROOT:-"(not found)"}"
echo "SQL host: $SQL_HOST (credentials redacted)"

LIVE=0
if [[ -n "$SQL_PASSWORD" ]]; then
  if [[ -n "${MCP_ROOT}" ]]; then
    if [[ ! -f "${MCP_ROOT}/dist/index.js" && -f "${MCP_ROOT}/package.json" ]]; then
      echo "Building SheikhGo-MCP..."
      (cd "$MCP_ROOT" && npm ci --ignore-scripts && npm run build)
    fi
    if [[ -f "${MCP_ROOT}/dist/index.js" ]]; then
      LIVE=1
    fi
  fi
fi

if [[ "$LIVE" -eq 1 ]]; then
  echo "Live SQL + MCP stdio E2E: ENABLED (require)"
  export SHEIKHGO_PHASE8_LIVE_SQL=require
  export SHEIKHGO_PHASE8_SQL_HOST="$SQL_HOST"
  export SHEIKHGO_PHASE8_SQL_USER="$SQL_USER"
  export SHEIKHGO_PHASE8_SQL_PASSWORD="$SQL_PASSWORD"
  export SHEIKHGO_PHASE8_MCP_ENTRY="${SHEIKHGO_PHASE8_MCP_ENTRY:-$MCP_ROOT/dist/index.js}"
  export SHEIKHGO_PHASE8_SQL_CONNECTION="Server=${SQL_HOST};Database=master;User Id=${SQL_USER};Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=False;"
  export SHEIKHGO_DB_CONNECTION="$SHEIKHGO_PHASE8_SQL_CONNECTION"

  echo "Waiting for SQL Server TCP..."
  for i in $(seq 1 60); do
    if command -v node >/dev/null 2>&1; then
      if node -e "
        const net=require('net');
        const raw=process.env.H||'127.0.0.1,1433';
        const parts=raw.split(',');
        const host=parts[0];
        const port=Number(parts[1]||1433);
        const s=net.connect(port, host, ()=>{s.end(); process.exit(0);});
        s.on('error',()=>process.exit(1));
        setTimeout(()=>process.exit(1),1500);
      " H="$SQL_HOST"; then
        echo "SQL TCP open."
        break
      fi
    elif nc -z "${SQL_HOST%%,*}" "${SQL_HOST##*,}" 2>/dev/null; then
      echo "SQL TCP open (nc)."
      break
    fi
    if [[ "$i" -eq 60 ]]; then
      echo "ERROR: SQL Server did not become ready"
      exit 1
    fi
    sleep 2
  done
else
  echo "Live SQL + MCP stdio E2E: SKIPPED (unit/security Phase8 still run)"
  echo "  Hint: set MSSQL_SA_PASSWORD + SHEIKHGO_MCP_ROOT with dist/index.js for live E2E."
  unset SHEIKHGO_PHASE8_LIVE_SQL || true
fi

echo "Running Phase 8 tests..."
dotnet test "$ROOT/Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj" \
  --filter "FullyQualifiedName~Phase8" \
  --logger "console;verbosity=normal" \
  --nologo

echo "Phase 8 CI complete (live=${LIVE})."
