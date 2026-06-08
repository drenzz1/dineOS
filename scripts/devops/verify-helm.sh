#!/usr/bin/env bash
# scripts/devops/verify-helm.sh
# Full kind-based smoke test for the dineOS Helm chart.
# Run from the repo root: bash scripts/devops/verify-helm.sh
#
# Requirements: kind, docker, helm (3.14+), kubectl
# The kind cluster is always deleted on exit, even on failure.

set -euo pipefail

CLUSTER=dineos-test
NAMESPACE=dineos
RELEASE=dineos
CHART=deploy/helm/dineos
VALUES=deploy/helm/dineos/values.local.yaml
PF_PORT=18080   # local port for kubectl port-forward
PF_PID=""
PASS=0
FAIL=0

# ── Colour helpers ────────────────────────────────────────────────────────────
GREEN='\033[0;32m'
RED='\033[0;31m'
CYAN='\033[0;36m'
RESET='\033[0m'

step()  { echo -e "\n${CYAN}→ [$1] $2${RESET}"; }
ok()    { echo -e "${GREEN}✓ $1${RESET}"; PASS=$((PASS + 1)); }
fail()  { echo -e "${RED}✗ $1${RESET}"; FAIL=$((FAIL + 1)); }

# ── Cleanup on exit ───────────────────────────────────────────────────────────
cleanup() {
  if [[ -n "$PF_PID" ]]; then
    kill "$PF_PID" 2>/dev/null || true
  fi
  if kind get clusters 2>/dev/null | grep -q "^${CLUSTER}$"; then
    echo -e "\n→ [10/10] Tearing down kind cluster"
    kind delete cluster --name "$CLUSTER"
  fi
  echo ""
  if [[ $FAIL -eq 0 ]]; then
    echo -e "${GREEN}✓ helm-smoke passed (all $((PASS)) checks)${RESET}"
  else
    echo -e "${RED}✗ helm-smoke FAILED ($FAIL check(s) failed, $PASS passed)${RESET}"
    exit 1
  fi
}
trap cleanup EXIT INT TERM

# ── Step 1 — Create kind cluster ──────────────────────────────────────────────
step "1/10" "Creating kind cluster '${CLUSTER}'"
kind create cluster --name "$CLUSTER"
kubectl config use-context "kind-${CLUSTER}"
ok "Cluster ready"

# ── Step 2 — Install nginx-ingress-controller ─────────────────────────────────
step "2/10" "Installing nginx-ingress-controller"
# Pinned to a fixed release tag — avoids non-reproducible 'main' branch and supply-chain risk.
# Bump this tag when upgrading the kind cluster's Kubernetes version.
kubectl apply -f \
  https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.12.0/deploy/static/provider/kind/deploy.yaml
kubectl wait \
  --namespace ingress-nginx \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller \
  --timeout=90s
ok "nginx-ingress-controller ready"

# ── Step 3 — Build images ─────────────────────────────────────────────────────
step "3/10" "Building images"
docker build -t dineos/api:do2 \
  -f backend/src/DineOS.Api/Dockerfile \
  ./backend
docker build -t dineos/web:do2 \
  ./frontend
ok "Images built"

# ── Step 4 — Load images into kind nodes ──────────────────────────────────────
step "4/10" "Loading images into kind nodes"
kind load docker-image dineos/api:do2 --name "$CLUSTER"
kind load docker-image dineos/web:do2 --name "$CLUSTER"
ok "Images loaded"

# ── Step 5 — Update Helm dependencies ─────────────────────────────────────────
step "5/10" "Downloading Helm dependencies"
# 'helm dependency build' uses Chart.lock (committed) to download exactly
# the pinned versions. 'helm dependency update' re-resolves constraints and
# can pick up newer patch versions, making the smoke test non-deterministic.
helm dependency build "$CHART"
ok "Dependencies downloaded"

# ── Step 6 — Install chart ────────────────────────────────────────────────────
step "6/10" "Installing Helm chart"
helm install "$RELEASE" "$CHART" \
  -f "$VALUES" \
  -n "$NAMESPACE" \
  --create-namespace
ok "Chart installed (REVISION 1)"

# ── Step 7 — Wait for rollouts ────────────────────────────────────────────────
step "7/10" "Waiting for rollouts (timeout 3 min each)"
kubectl rollout status "deployment/${RELEASE}-api"      -n "$NAMESPACE" --timeout=180s
kubectl rollout status "deployment/${RELEASE}-frontend" -n "$NAMESPACE" --timeout=180s
ok "Both Deployments rolled out"

# ── Step 8 — Port-forward ingress controller ──────────────────────────────────
step "8/10" "Health check — GET /api/v1/health"
kubectl port-forward \
  -n ingress-nginx \
  svc/ingress-nginx-controller \
  "${PF_PORT}:80" &
PF_PID=$!
# Give the port-forward a moment to bind
sleep 3

HEALTH=$(curl -sf --max-time 10 \
  -H "Host: dineos.local" \
  "http://localhost:${PF_PORT}/api/v1/health" || echo "FAILED")

echo "$HEALTH"
if echo "$HEALTH" | grep -q '"status"'; then
  ok "API health check returned expected payload"
else
  fail "API health check did not return expected payload"
fi

# ── Step 9 — Frontend check ───────────────────────────────────────────────────
step "9/10" "Frontend check — GET /"
HTTP_CODE=$(curl -so /dev/null --max-time 10 \
  -w "%{http_code}" \
  -H "Host: dineos.local" \
  "http://localhost:${PF_PORT}/")

echo "HTTP ${HTTP_CODE}"
if [[ "$HTTP_CODE" == "200" ]]; then
  ok "Frontend returned HTTP 200"
else
  fail "Frontend returned HTTP ${HTTP_CODE} (expected 200)"
fi

# Cleanup is handled by the trap (step 10 label printed there)
