#!/usr/bin/env bash
#
# Post-deploy Azure Monitor alert setup, shared by the staging and production workflows.
# Resolves the deployed Application Insights component and the public web Container App, then
# applies infra/alerts.bicep (email action group + availability web test + availability/5xx alerts).
#
# Applying is idempotent: `az deployment group create` upserts the alert resources by name, so this
# can run after every deploy. It reuses the same discovery approach as verify-deployment.sh.
#
# Inputs (environment variables):
#   RG                Azure resource group the app was deployed to                (required)
#   ENVIRONMENT_NAME  Short env name used in resource names (staging|production)  (required)
#   ALERT_EMAIL       Email address that receives alerts                          (default: momohammad@microsoft.com)
#   WEB_APP_MATCH     Substring used to find the web container app                (default: "web")
#   TEMPLATE_FILE     Path to the Bicep module                                    (default: repo infra/alerts.bicep)
#
set -euo pipefail

RG="${RG:?resource group (RG) is required}"
ENVIRONMENT_NAME="${ENVIRONMENT_NAME:?environment name (ENVIRONMENT_NAME) is required, e.g. staging or production}"
ALERT_EMAIL="${ALERT_EMAIL:-momohammad@microsoft.com}"
WEB_APP_MATCH="${WEB_APP_MATCH:-web}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE_FILE="${TEMPLATE_FILE:-${SCRIPT_DIR}/../../infra/alerts.bicep}"

if [ ! -f "$TEMPLATE_FILE" ]; then
  echo "::error::Bicep template not found at '${TEMPLATE_FILE}'."
  exit 1
fi

echo "Configuring Azure Monitor alerts in resource group '${RG}' (env: ${ENVIRONMENT_NAME})..."

# 1. Application Insights component — created by Aspire and tagged aspire-resource-name=insights.
echo "Resolving the Application Insights component..."
AI_ID=""
for i in $(seq 1 12); do
  AI_ID=$(az resource list -g "$RG" --resource-type "Microsoft.Insights/components" \
    --query "[?tags.\"aspire-resource-name\"=='insights'].id | [0]" -o tsv 2>/dev/null || true)
  if [ -z "$AI_ID" ] || [ "$AI_ID" = "null" ]; then
    # Fallback: any Application Insights component in the resource group.
    AI_ID=$(az resource list -g "$RG" --resource-type "Microsoft.Insights/components" \
      --query "[0].id" -o tsv 2>/dev/null || true)
  fi
  if [ -n "$AI_ID" ] && [ "$AI_ID" != "null" ]; then break; fi
  echo "  ...waiting for Application Insights ($i/12)"
  sleep 10
done
if [ -z "$AI_ID" ] || [ "$AI_ID" = "null" ]; then
  echo "::error::Could not find an Application Insights component in resource group '${RG}'."
  exit 1
fi
echo "  Application Insights: ${AI_ID}"

# 2. Web Container App (public ingress) — resource id + ingress FQDN.
echo "Resolving the '${WEB_APP_MATCH}' Container App (id + ingress FQDN)..."
CA_ID=""
FQDN=""
for i in $(seq 1 30); do
  CA_ID=$(az containerapp list -g "$RG" \
    --query "[?contains(name, '${WEB_APP_MATCH}')].id | [0]" -o tsv 2>/dev/null || true)
  FQDN=$(az containerapp list -g "$RG" \
    --query "[?contains(name, '${WEB_APP_MATCH}')].properties.configuration.ingress.fqdn | [0]" -o tsv 2>/dev/null || true)
  if [ -n "$CA_ID" ] && [ "$CA_ID" != "null" ] && [ -n "$FQDN" ] && [ "$FQDN" != "null" ]; then break; fi
  echo "  ...waiting for the web Container App ($i/30)"
  sleep 10
done
if [ -z "$CA_ID" ] || [ "$CA_ID" = "null" ] || [ -z "$FQDN" ] || [ "$FQDN" = "null" ]; then
  echo "::error::Could not resolve the web Container App id/FQDN in resource group '${RG}'."
  exit 1
fi
echo "  Web Container App: ${CA_ID}"
echo "  Web FQDN: ${FQDN}"

# 3. Apply the alert resources (idempotent upsert).
echo "Applying alert resources from ${TEMPLATE_FILE}..."
az deployment group create \
  --name "taqyeem-alerts-${ENVIRONMENT_NAME}" \
  --resource-group "$RG" \
  --template-file "$TEMPLATE_FILE" \
  --parameters \
    environmentName="$ENVIRONMENT_NAME" \
    appInsightsId="$AI_ID" \
    webAppFqdn="$FQDN" \
    webContainerAppId="$CA_ID" \
    notificationEmail="$ALERT_EMAIL" \
  --only-show-errors \
  -o none

echo "Azure Monitor alerts configured for '${ENVIRONMENT_NAME}' (recipient: ${ALERT_EMAIL})."
