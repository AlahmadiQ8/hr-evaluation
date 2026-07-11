#!/usr/bin/env bash
# Resets the demo database to its pristine seeded state by removing the Aspire SQL data
# volume. Run this before an E2E suite so the mutating workflow specs start from a fresh seed.
#
#   ./e2e/reset-demo-data.sh
#
# The app must be stopped first (aspire stop). The next `aspire run` re-applies the seed.
set -euo pipefail

volume=$(docker volume ls --format '{{.Name}}' | grep -E 'apphost-.*-sql-data' || true)

if [[ -z "${volume}" ]]; then
  echo "No Taqyeem SQL data volume found (already clean, or the app has never run)."
  exit 0
fi

echo "Removing SQL data volume: ${volume}"
docker volume rm "${volume}"
echo "Done. The next 'aspire run' will re-seed the demo data."
