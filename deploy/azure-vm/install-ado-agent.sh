#!/usr/bin/env bash
# Install Azure DevOps self-hosted agent on SheikhGo VM (build+deploy on the VM).
# One-time. Requires a PAT with Agent Pools (Read & manage) scope.
#
# Usage (on the VM as azureuser):
#   export AZP_URL='https://dev.azure.com/Alirazatahir'
#   export AZP_TOKEN='***PAT***'
#   export AZP_POOL='SheikhGo-VM'
#   export AZP_AGENT_NAME='sheikhgo-vm'
#   ./install-ado-agent.sh
set -euo pipefail

AZP_URL="${AZP_URL:?Set AZP_URL e.g. https://dev.azure.com/your-org}"
AZP_TOKEN="${AZP_TOKEN:?Set AZP_TOKEN (PAT)}"
AZP_POOL="${AZP_POOL:-SheikhGo-VM}"
AZP_AGENT_NAME="${AZP_AGENT_NAME:-sheikhgo-vm}"
AGENT_DIR="${AGENT_DIR:-/opt/sheikhgo-api/ado-agent}"

mkdir -p "$AGENT_DIR"
cd "$AGENT_DIR"

if [[ ! -f ./config.sh ]]; then
  echo "==> Downloading Azure Pipelines agent"
  curl -fsSL -o agent.tar.gz \
    https://download.agent.dev.azure.com/agent/4.255.0/vsts-agent-linux-x64-4.255.0.tar.gz
  tar zxvf agent.tar.gz
  rm -f agent.tar.gz
fi

if [[ ! -f .agent ]]; then
  echo "==> Configuring agent for pool $AZP_POOL"
  ./config.sh --unattended \
    --url "$AZP_URL" \
    --auth pat \
    --token "$AZP_TOKEN" \
    --pool "$AZP_POOL" \
    --agent "$AZP_AGENT_NAME" \
    --work _work \
    --replace \
    --acceptTeeEula
fi

echo "==> Installing + starting systemd service"
sudo ./svc.sh install azureuser
sudo ./svc.sh start
sudo ./svc.sh status || true
echo "==> Agent installed. Create pipeline from azure-pipelines.yml targeting pool '$AZP_POOL'."
echo "    Unset AZP_TOKEN from your shell history when done."
