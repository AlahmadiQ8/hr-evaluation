#!/usr/bin/env bash
#
# Post-deploy verification shared by the staging and production workflows.
# Resolves the deployed Blazor Web app's ingress FQDN, waits for /health, and (optionally)
# asserts that /version reports the exact release that was supposed to be deployed.
#
# Inputs (environment variables):
#   RG                Azure resource group the app was deployed to        (required)
#   EXPECTED_VERSION  Version string /version must report to pass         (optional)
#   WEB_APP_MATCH     Substring used to find the web container app        (default: "web")
#
set -euo pipefail

RG="${RG:?resource group (RG) is required}"
EXPECTED_VERSION="${EXPECTED_VERSION:-}"
WEB_APP_MATCH="${WEB_APP_MATCH:-web}"

echo "Resolving the '${WEB_APP_MATCH}' container app ingress FQDN in resource group '${RG}'..."
FQDN=""
for i in $(seq 1 30); do
  FQDN=$(az containerapp list -g "$RG" \
    --query "[?contains(name, '${WEB_APP_MATCH}')].properties.configuration.ingress.fqdn | [0]" \
    -o tsv 2>/dev/null || true)
  if [ -n "$FQDN" ] && [ "$FQDN" != "null" ]; then break; fi
  echo "  ...waiting for the web container app ingress ($i/30)"
  sleep 10
done

if [ -z "$FQDN" ] || [ "$FQDN" = "null" ]; then
  echo "::error::Could not resolve the web app ingress FQDN in resource group '${RG}'."
  exit 1
fi

BASE="https://${FQDN}"
echo "Web app base URL: ${BASE}"

echo "Waiting for /health to report healthy..."
for i in $(seq 1 30); do
  if curl -fsS "${BASE}/health" >/dev/null 2>&1; then
    echo "  /health is healthy."
    break
  fi
  if [ "$i" = "30" ]; then
    echo "::error::${BASE}/health did not become healthy in time."
    exit 1
  fi
  sleep 10
done

echo "Reading /version..."
VERSION_JSON=$(curl -fsS "${BASE}/version")
echo "  ${BASE}/version -> ${VERSION_JSON}"
DEPLOYED_VERSION=$(printf '%s' "$VERSION_JSON" | python3 -c "import sys, json; print(json.load(sys.stdin).get('version', ''))")

if [ -n "$EXPECTED_VERSION" ]; then
  if [ "$DEPLOYED_VERSION" != "$EXPECTED_VERSION" ]; then
    echo "::error::Deployed version '${DEPLOYED_VERSION}' does not match the expected release '${EXPECTED_VERSION}'."
    exit 1
  fi
  echo "Verified: the deployed version matches the expected release '${EXPECTED_VERSION}'."
fi

echo "Deployment verification succeeded (${BASE})."
