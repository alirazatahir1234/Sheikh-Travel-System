#!/usr/bin/env bash
# TravelNexus Phase 8 — live SQL end-to-end tests (repository + handler level).
# Creates a throwaway database SheikhGo_TravelNexus_E2E_<stamp> on the CI SQL Server, applies
# Backend/Database/CompleteSchema.sql plus every registered migration, runs the three brief workflows for two
# companies through the real MediatR handlers and Dapper repositories, then drops the database.
# Same SQL env conventions as phase8-live-sql-e2e.sh. Never echoes connection strings or passwords.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

SQL_PASSWORD="${SHEIKHGO_PHASE8_SQL_PASSWORD:-${MSSQL_SA_PASSWORD:-}}"
SQL_HOST="${SHEIKHGO_PHASE8_SQL_HOST:-127.0.0.1,1433}"
SQL_USER="${SHEIKHGO_PHASE8_SQL_USER:-sa}"

echo "== TravelNexus live SQL E2E =="
echo "Repo: $ROOT"
echo "SQL host: $SQL_HOST (credentials redacted)"

if [[ -z "$SQL_PASSWORD" ]]; then
  echo "No SQL password (SHEIKHGO_PHASE8_SQL_PASSWORD / MSSQL_SA_PASSWORD) — TravelNexus live E2E SKIPPED; wiring test still runs."
  unset SHEIKHGO_TRAVELNEXUS_SQL_CONNECTION || true
  unset SHEIKHGO_TRAVELNEXUS_REQUIRE_LIVE_SQL || true
else
  echo "TravelNexus live E2E: ENABLED (require)"
  # Server-level connection: the fixture refuses anything but master and creates its own database.
  export SHEIKHGO_TRAVELNEXUS_SQL_CONNECTION="Server=${SQL_HOST};Database=master;User Id=${SQL_USER};Password=${SQL_PASSWORD};TrustServerCertificate=True;Encrypt=False;"
  export SHEIKHGO_TRAVELNEXUS_REQUIRE_LIVE_SQL=1
fi

echo "Running TravelNexus live SQL tests..."
dotnet test "$ROOT/Backend/SheikhTravelSystem.Tests/SheikhTravelSystem.Tests.csproj" \
  --filter "FullyQualifiedName~TravelNexus.LiveSql" \
  --logger "console;verbosity=normal" \
  --nologo

echo "TravelNexus live SQL E2E complete."
