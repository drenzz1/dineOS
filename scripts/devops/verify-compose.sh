#!/usr/bin/env sh
# verify-compose.sh — Smoke-checks the running DineOS dev stack.
#
# Usage (from repo root):
#   sh scripts/devops/verify-compose.sh
#
# Exits 0 when all checks pass, non-zero when any required check fails.
# Checks are SKIPPED (not failed) when the target service is not running.

set -u

# ── Colour helpers (disabled when stdout is not a TTY) ───────────
if [ -t 1 ]; then
  C_GREEN='\033[0;32m'; C_RED='\033[0;31m'
  C_YELLOW='\033[1;33m'; C_BOLD='\033[1m'; C_RESET='\033[0m'
else
  C_GREEN=''; C_RED=''; C_YELLOW=''; C_BOLD=''; C_RESET=''
fi

failures=0

step() { printf "\n${C_BOLD}==> %s${C_RESET}\n" "$1"; }
ok()   { printf "  ${C_GREEN}PASS${C_RESET}  %s\n" "$1"; }
bad()  { printf "  ${C_RED}FAIL${C_RESET}  %s\n" "$1"; failures=$((failures + 1)); }
skip() { printf "  ${C_YELLOW}SKIP${C_RESET}  %s\n" "$1"; }

# ── 1. Compose config validation ─────────────────────────────────
step "1/3  Compose config"
if docker compose config -q 2>/dev/null; then
  ok "docker compose config is valid"
else
  bad "docker compose config reported errors — run 'docker compose config' for details"
fi

# ── 2. Service status table ──────────────────────────────────────
step "2/3  Service status"
printf "  %-22s  %-10s  %-12s\n" "SERVICE" "STATE" "HEALTH"
printf "  %-22s  %-10s  %-12s\n" "──────────────────────" "──────────" "────────────"
docker compose ps --format '{{.Service}}|{{.State}}|{{.Health}}' 2>/dev/null \
  | awk -F'|' '{h=($3==""?"—":$3); printf "  %-22s  %-10s  %-12s\n",$1,$2,h}'

# Collect names of services that currently have containers (any state)
running_svcs=$(docker compose ps --format '{{.Service}}' 2>/dev/null | tr '\n' ':')
service_running() {
  case ":${running_svcs}:" in *":${1}:"*) return 0;; *) return 1;; esac
}

# ── 3. Health endpoint checks ────────────────────────────────────
step "3/3  Health checks"

check() {
  label="$1"; url="$2"; svc="$3"
  if ! service_running "$svc"; then
    skip "${label}  (service '${svc}' not running)"
    return
  fi
  http=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$url" 2>/dev/null) || http="000"
  case "$http" in
    2*) ok  "${label}  [${http}]  ${url}" ;;
    *)  bad "${label}  [${http}]  ${url}" ;;
  esac
}

check "API health (via Nginx)"   "http://localhost/api/v1/health"                                       "nginx"
check "Keycloak OIDC discovery"  "http://localhost:8080/realms/dineos/.well-known/openid-configuration" "keycloak"
check "Loki readiness"           "http://localhost:3100/ready"                                          "loki"
check "Grafana health"           "http://localhost:4000/api/health"                                     "grafana"

# ── Result ───────────────────────────────────────────────────────
printf '\n'
if [ "$failures" -eq 0 ]; then
  printf "${C_GREEN}All checks passed.${C_RESET}\n"
  exit 0
else
  printf "${C_RED}%d check(s) failed.${C_RESET}\n" "$failures"
  exit 1
fi
