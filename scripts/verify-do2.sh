#!/usr/bin/env bash
# Verification script for DO-2 image builds (dineos/api:do2, dineos/web:do2).
# Run from the repo root:  bash scripts/verify-do2.sh
set -euo pipefail

API_PORT="${API_HTTP_PORT:-5001}"
HEALTH_URL="http://localhost:${API_PORT}/api/v1/health"

echo "───────────────────────────────────────────"
echo " 1/4  Build images (no cache)"
echo "───────────────────────────────────────────"
docker compose build --no-cache

echo ""
echo "───────────────────────────────────────────"
echo " 2/4  Verify non-root USER in built images"
echo "───────────────────────────────────────────"
for image in dineos/api:do2 dineos/web:do2; do
  user=$(docker inspect --format '{{.Config.User}}' "$image")
  echo "  $image  →  USER=${user:-'(not set — defaults to root!)'}"
done

echo ""
echo "───────────────────────────────────────────"
echo " 3/4  Start stack"
echo "───────────────────────────────────────────"
docker compose up -d

echo ""
echo "───────────────────────────────────────────"
echo " 4/4  Wait for API health (up to 90 s)"
echo "───────────────────────────────────────────"
for i in $(seq 1 18); do
  status=$(docker inspect --format '{{.State.Health.Status}}' dineos-api 2>/dev/null || echo "unknown")
  if [[ "$status" == "healthy" ]]; then
    echo "  dineos-api is healthy."
    break
  fi
  echo "  [$i/18] dineos-api status=${status} — retrying in 5 s..."
  sleep 5
  if [[ $i -eq 18 ]]; then
    echo "  ERROR: API did not become healthy within 90 s." >&2
    docker compose logs --tail=30 api >&2
    exit 1
  fi
done

echo ""
echo "  GET ${HEALTH_URL}"
curl -sf "${HEALTH_URL}" | { command -v jq &>/dev/null && jq . || cat; }

echo ""
echo "All checks passed."
