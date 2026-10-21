#!/usr/bin/env bash
# Deploy SheikhGo API to IBM Cloud.
#
# IBM deploy details are NOT in this repo yet. Set ADO variables, then choose a method:
#   IBM_DEPLOY_METHOD = ssh | codeengine | cloudfoundry
#
# See docs/deploy/ibm-vercel-cicd.md for what Ali must provide.
set -euo pipefail

ROOT="${BUILD_SOURCESDIRECTORY:-$(cd "$(dirname "$0")/../.." && pwd)}"
API_CSPROJ="$ROOT/Backend/SheikhTravelSystem.API/SheikhTravelSystem.API.csproj"
PUBLISH_DIR="${AGENT_TEMPDIRECTORY:-/tmp}/sheikhgo-api-publish"

METHOD="${IBM_DEPLOY_METHOD:-}"
if [[ -z "$METHOD" ]]; then
  echo "##vso[task.logissue type=error]IBM_DEPLOY_METHOD is not set."
  echo "Missing IBM deployment config. Set ADO variables documented in docs/deploy/ibm-vercel-cicd.md"
  echo "Required at minimum: IBM_DEPLOY_METHOD (ssh|codeengine|cloudfoundry) plus method-specific secrets."
  exit 1
fi

echo "=== Building API (dotnet publish) ==="
mkdir -p "$PUBLISH_DIR"
dotnet publish "$API_CSPROJ" -c Release -o "$PUBLISH_DIR" --verbosity minimal

case "$METHOD" in
  ssh)
    : "${IBM_HOST:?IBM_HOST required for ssh method}"
    : "${IBM_SSH_USER:?IBM_SSH_USER required for ssh method}"
    : "${IBM_SSH_KEY:?IBM_SSH_KEY required for ssh method (private key contents)}"
    REMOTE_PATH="${IBM_REMOTE_PATH:-/opt/sheikhgo-api/current}"
    SERVICE_NAME="${IBM_SERVICE_NAME:-sheikhgo-api}"

    KEY_FILE="$(mktemp)"
    trap 'rm -f "$KEY_FILE"' EXIT
    printf '%s\n' "$IBM_SSH_KEY" > "$KEY_FILE"
    chmod 600 "$KEY_FILE"

    echo "=== Syncing publish output to ${IBM_SSH_USER}@${IBM_HOST}:${REMOTE_PATH} ==="
    rsync -az --delete \
      -e "ssh -i $KEY_FILE -o StrictHostKeyChecking=accept-new" \
      "$PUBLISH_DIR/" \
      "${IBM_SSH_USER}@${IBM_HOST}:${REMOTE_PATH}/"

    echo "=== Restarting remote service ${SERVICE_NAME} ==="
    ssh -i "$KEY_FILE" -o StrictHostKeyChecking=accept-new \
      "${IBM_SSH_USER}@${IBM_HOST}" \
      "sudo systemctl restart '${SERVICE_NAME}' && sudo systemctl is-active '${SERVICE_NAME}'"
    ;;

  codeengine)
    : "${IBM_API_KEY:?IBM_API_KEY required for codeengine}"
    : "${IBM_REGION:?IBM_REGION required for codeengine}"
    : "${IBM_RESOURCE_GROUP:?IBM_RESOURCE_GROUP required for codeengine}"
    : "${IBM_CE_PROJECT:?IBM_CE_PROJECT required for codeengine}"
    : "${IBM_APP_NAME:?IBM_APP_NAME required for codeengine}"

    echo "=== IBM Code Engine deploy (placeholder) ==="
    echo "Install ibmcloud CLI + container-registry/code-engine plugins, then:"
    echo "  ibmcloud login --apikey \"\$IBM_API_KEY\" -r \"\$IBM_REGION\" -g \"\$IBM_RESOURCE_GROUP\""
    echo "  ibmcloud ce project select -n \"\$IBM_CE_PROJECT\""
    echo "  # Build/push image from Dockerfile.railway (or IBM-specific Dockerfile), then:"
    echo "  ibmcloud ce application update -n \"\$IBM_APP_NAME\" --image <registry/image:tag>"
    echo "##vso[task.logissue type=error]Code Engine steps not fully wired — provide image registry + exact app update command."
    exit 1
    ;;

  cloudfoundry)
    : "${IBM_API_KEY:?IBM_API_KEY required for cloudfoundry}"
    : "${IBM_CF_ORG:?IBM_CF_ORG required for cloudfoundry}"
    : "${IBM_CF_SPACE:?IBM_CF_SPACE required for cloudfoundry}"
    : "${IBM_APP_NAME:?IBM_APP_NAME required for cloudfoundry}"
    : "${IBM_REGION:?IBM_REGION required for cloudfoundry}"

    echo "=== IBM Cloud Foundry deploy (placeholder) ==="
    echo "  ibmcloud login --apikey \"\$IBM_API_KEY\" -r \"\$IBM_REGION\""
    echo "  ibmcloud target -o \"\$IBM_CF_ORG\" -s \"\$IBM_CF_SPACE\""
    echo "  ibmcloud cf push \"\$IBM_APP_NAME\" -p \"$PUBLISH_DIR\" ..."
    echo "##vso[task.logissue type=error]Cloud Foundry push not fully wired — provide manifest.yml + runtime buildpack."
    exit 1
    ;;

  *)
    echo "##vso[task.logissue type=error]Unknown IBM_DEPLOY_METHOD='$METHOD' (use ssh|codeengine|cloudfoundry)"
    exit 1
    ;;
esac

echo "=== IBM deploy step finished ==="
