#!/usr/bin/env bash
# Apply conservative daily quotas + $20/$50 budget alerts for Maps Geocoding + Places.
# Prerequisites:
#   gcloud auth login
#   gcloud config set project <PROJECT_ID>   # Maps billing project (often techbirdsfly)
#   Billing account linked to that project
#
# Usage:
#   export GCP_MAPS_PROJECT=techbirdsfly
#   export GCP_BILLING_ACCOUNT=XXXXXX-XXXXXX-XXXXXX   # from: gcloud billing accounts list
#   ./docs/production-readiness/scripts/apply-maps-quotas-and-budgets.sh
#
# Safe defaults for ~10 vehicles after cost-cut (raise later if UI needs more):
#   Geocoding requests/day: 2000
#   Places Nearby Search (legacy) requests/day: 500
#   Places API New Nearby: left higher (user-initiated panel only) — set in Console if needed
#
# NOTE: Some Maps SKU quotas are Console-only; this script sets what gcloud exposes and
# always creates/updates billing budgets. Confirm in Console → APIs & Services → Quotas.

set -euo pipefail

PROJECT="${GCP_MAPS_PROJECT:-$(gcloud config get-value project 2>/dev/null)}"
BILLING_ACCOUNT="${GCP_BILLING_ACCOUNT:-}"
GEOCODE_DAY="${GCP_GEOCODE_DAY_QUOTA:-2000}"
PLACES_LEGACY_DAY="${GCP_PLACES_LEGACY_DAY_QUOTA:-500}"
ALERT_EMAIL="${GCP_BUDGET_ALERT_EMAIL:-alisheikh14125@gmail.com}"

if [[ -z "$PROJECT" || "$PROJECT" == "(unset)" ]]; then
  echo "Set GCP_MAPS_PROJECT or gcloud config set project" >&2
  exit 1
fi

echo "Project: $PROJECT"

# Verify auth works
if ! gcloud projects describe "$PROJECT" >/dev/null 2>&1; then
  echo "gcloud auth failed for project $PROJECT — run: gcloud auth login" >&2
  exit 1
fi

echo "Attempting consumer quota updates (may no-op if SKU is Console-only)..."
# Geocoding API — requests per day (metric names vary by API version)
set +e
gcloud alpha services quota update \
  --service=geocoding-backend.googleapis.com \
  --consumer="projects/$PROJECT" \
  --metric=geocoding.googleapis.com/mapsum/requests \
  --unit=1/d/{project} \
  --value="$GEOCODE_DAY" 2>&1 | tee /tmp/gcloud-quota-geocode.txt
gcloud alpha services quota update \
  --service=places-backend.googleapis.com \
  --consumer="projects/$PROJECT" \
  --metric=places.googleapis.com/nearbysearch/requests \
  --unit=1/d/{project} \
  --value="$PLACES_LEGACY_DAY" 2>&1 | tee /tmp/gcloud-quota-places.txt
set -e

echo ""
echo "If quota update failed, set manually:"
echo "  https://console.cloud.google.com/google/maps-apis/quotas?project=$PROJECT"
echo "  Geocoding API → Requests per day → $GEOCODE_DAY"
echo "  Places API → Nearby Search requests per day → $PLACES_LEGACY_DAY"

if [[ -z "$BILLING_ACCOUNT" ]]; then
  echo ""
  echo "Listing billing accounts (pick one and re-run with GCP_BILLING_ACCOUNT=...):"
  gcloud billing accounts list || true
  echo "Budgets skipped until GCP_BILLING_ACCOUNT is set."
  echo "Manual: https://console.cloud.google.com/billing/budgets?project=$PROJECT"
  exit 0
fi

# Create two budgets if missing (idempotent-ish: list then create)
create_budget() {
  local name="$1" amount="$2"
  local existing
  existing=$(gcloud billing budgets list --billing-account="$BILLING_ACCOUNT" --filter="displayName:$name" --format='value(name)' 2>/dev/null | head -1 || true)
  if [[ -n "$existing" ]]; then
    echo "Budget exists: $name ($existing)"
    return 0
  fi
  gcloud billing budgets create \
    --billing-account="$BILLING_ACCOUNT" \
    --display-name="$name" \
    --budget-amount="${amount}" \
    --threshold-rule=percent=50 \
    --threshold-rule=percent=90 \
    --threshold-rule=percent=100 \
    --filter-projects="projects/$(gcloud projects describe "$PROJECT" --format='value(projectNumber)')" \
    --notifications-rule-monitoring-notification-channels= \
    2>&1 || {
      echo "CLI budget create failed for $name — use Console:"
      echo "  https://console.cloud.google.com/billing/$BILLING_ACCOUNT/budgets"
    }
}

create_budget "SheikhGo Maps \$20" "20USD"
create_budget "SheikhGo Maps \$50" "50USD"

echo ""
echo "Done. Also set email alerts on the budget in Console if CLI skipped notification channels."
echo "Alert email hint: $ALERT_EMAIL"
