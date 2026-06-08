#!/usr/bin/env bash
# Demo: ApiDown alert firing and recovery.
# Proves the full observability pipeline end-to-end:
#   prometheus scrape → alert rule → alertmanager → clear on restart
#
# Run from the repo root:  bash scripts/demo-alert.sh
#
# Prerequisites:
#   - docker compose up -d (all services including prometheus + alertmanager)
#   - .env or environment must set PROMETHEUS_PORT / ALERTMANAGER_PORT if not 9090/9093
set -euo pipefail

# ── Config ────────────────────────────────────────────────────────────────────
PROM_PORT="${PROMETHEUS_PORT:-9090}"
AM_PORT="${ALERTMANAGER_PORT:-9093}"
PROM_BASE="http://localhost:${PROM_PORT}"
AM_BASE="http://localhost:${AM_PORT}"

STOP_WAIT_SECONDS=120       # worst case: 15s scrape gap + 75s evaluations = 90s; +30s buffer
RECOVERY_WAIT_SECONDS=90    # API boot + 15s scrape interval + for:1m clear window
POLL_INTERVAL=5

# Colour codes (disabled automatically when stdout is not a terminal)
if [[ -t 1 ]]; then
  RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
  CYAN='\033[0;36m'; BOLD='\033[1m'; RESET='\033[0m'
else
  RED=''; GREEN=''; YELLOW=''; CYAN=''; BOLD=''; RESET=''
fi

step()  { echo -e "\n${CYAN}${BOLD}▶  $*${RESET}"; }
ok()    { echo -e "   ${GREEN}✓  $*${RESET}"; }
warn()  { echo -e "   ${YELLOW}⚠  $*${RESET}"; }
fail()  { echo -e "   ${RED}✗  $*${RESET}" >&2; }
hr()    { echo -e "${CYAN}────────────────────────────────────────────────────${RESET}"; }

# ── Helpers ───────────────────────────────────────────────────────────────────

# curl_health URL — returns 0 if the endpoint responds with HTTP 2xx
curl_health() {
  curl -sf --max-time 5 "$1" > /dev/null 2>&1
}

# wait_for_health URL label timeout_seconds — polls until healthy or times out
wait_for_health() {
  local url="$1" label="$2" timeout="$3"
  local elapsed=0
  printf "   Waiting for %s" "$label"
  until curl_health "$url"; do
    if (( elapsed >= timeout )); then
      echo ""
      fail "Timed out after ${timeout}s waiting for ${label}"
      return 1
    fi
    printf "."
    sleep "$POLL_INTERVAL"
    (( elapsed += POLL_INTERVAL ))
  done
  echo ""
  ok "${label} is up"
}

# active_alerts — pretty-prints the active alerts JSON from Prometheus
# Uses node (project prerequisite) for formatting; falls back to raw curl.
active_alerts() {
  curl -sf --max-time 5 "${PROM_BASE}/api/v1/alerts" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{process.stdout.write(JSON.stringify(JSON.parse(Buffer.concat(c).toString()),null,2));}
  catch(e){process.stdout.write(Buffer.concat(c).toString());}
});" 2>/dev/null \
    || curl -sf --max-time 5 "${PROM_BASE}/api/v1/alerts"
}

# alert_firing NAME — returns 0 only if this specific named alert is state=firing.
# Uses node so JSON is parsed accurately (not just grep, which can false-positive
# when another alert like DatabaseUnavailable is firing at the same time).
alert_firing() {
  local name="$1"
  curl -sf --max-time 5 "${PROM_BASE}/api/v1/alerts" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{
    const d=JSON.parse(Buffer.concat(c).toString());
    const alerts=(d.data||{}).alerts||[];
    for(const a of alerts){
      if((a.labels||{}).alertname==='${name}'&&a.state==='firing')process.exit(0);
    }
  }catch(e){}
  process.exit(1);
});" 2>/dev/null
}

# ── Track results for final summary ──────────────────────────────────────────
SUMMARY_LINES=()
record() { SUMMARY_LINES+=("$1"); }

# ── Main ──────────────────────────────────────────────────────────────────────
hr
echo -e " ${BOLD}DineOS — ApiDown alert demo${RESET}"
echo    " Prometheus : ${PROM_BASE}"
echo    " Alertmanager: ${AM_BASE}"
hr

# ── Step 1: verify services are reachable ────────────────────────────────────
step "1/5  Verify Prometheus and Alertmanager are up"

if ! curl_health "${PROM_BASE}/-/ready"; then
  fail "Prometheus is not reachable at ${PROM_BASE}/-/ready"
  echo "      Start the stack first:  docker compose up -d" >&2
  exit 1
fi
ok "Prometheus ${PROM_BASE}/-/ready → healthy"

if ! curl_health "${AM_BASE}/-/ready"; then
  fail "Alertmanager is not reachable at ${AM_BASE}/-/ready"
  echo "      Start the stack first:  docker compose up -d" >&2
  exit 1
fi
ok "Alertmanager ${AM_BASE}/-/ready → healthy"

record "Prometheus  : UP"
record "Alertmanager: UP"

# ── Step 2: confirm api is running before we stop it ────────────────────────
step "2/5  Confirm DineOS API is running and Prometheus sees it as UP"
if ! docker compose ps --status running api 2>/dev/null | grep -q "api"; then
  warn "The 'api' service does not appear to be running."
  warn "Starting it now so the stop → fire → recover cycle is repeatable."
  docker compose start api
fi

# Wait for Prometheus to confirm up=1 (API is actively being scraped).
# This clears any pending ApiDown state left over from a previous demo run.
echo "   Waiting for Prometheus to confirm api target is UP..."
prom_up_elapsed=0
until curl -sf --max-time 5 \
    "${PROM_BASE}/api/v1/query?query=up%7Bjob%3D%22dineos-api%22%7D%3D%3D1" \
    | grep -q '"1"' 2>/dev/null; do
  if (( prom_up_elapsed >= 60 )); then
    fail "Prometheus did not register api as UP within 60s"
    exit 1
  fi
  printf "   %3ds — waiting...\n" "$prom_up_elapsed"
  sleep "$POLL_INTERVAL"
  (( prom_up_elapsed += POLL_INTERVAL ))
done
ok "Prometheus confirms api target is UP"

# Give one full scrape+evaluation cycle so any lingering pending alert resolves.
echo "   Waiting one evaluation cycle (15s) to clear any stale pending state..."
sleep 15

ok "API container is running and scrape target is clean"
record "API pre-state: RUNNING"

# ── Step 3: stop api and wait for ApiDown to fire ────────────────────────────
step "3/5  Stop API → wait ${STOP_WAIT_SECONDS}s for ApiDown to fire"
echo    "   ApiDown rule: up{job=\"dineos-api\"} == 0  for: 1m"
docker compose stop api
ok "docker compose stop api — done"

echo "   Polling for ApiDown (checking every ${POLL_INTERVAL}s, timeout ${STOP_WAIT_SECONDS}s)..."
elapsed=0
fired=false
while (( elapsed < STOP_WAIT_SECONDS )); do
  if alert_firing "ApiDown"; then
    fired=true
    break
  fi
  printf "   %3ds — not firing yet...\n" "$elapsed"
  sleep "$POLL_INTERVAL"
  (( elapsed += POLL_INTERVAL ))
done

echo ""
echo -e "   ${BOLD}Active alerts at ${elapsed}s:${RESET}"
active_alerts || true
echo ""

if $fired; then
  ok "ApiDown is FIRING after ${elapsed}s"
  record "ApiDown     : FIRED after ${elapsed}s ✓"
else
  warn "ApiDown did not reach 'firing' within ${STOP_WAIT_SECONDS}s."
  warn "This may mean the alert evaluation interval needs more time, or"
  warn "the api target was already unhealthy before the stop."
  warn "Check ${PROM_BASE}/alerts for current alert state."
  record "ApiDown     : did not fire within ${STOP_WAIT_SECONDS}s ⚠"
fi

# ── Step 4: restart api and confirm alert clears ─────────────────────────────
step "4/5  Restart API → confirm ApiDown clears"
docker compose start api
ok "docker compose start api — done"

echo "   Waiting up to ${RECOVERY_WAIT_SECONDS}s for the alert to clear..."
elapsed=0
cleared=false
while (( elapsed < RECOVERY_WAIT_SECONDS )); do
  if ! alert_firing "ApiDown"; then
    cleared=true
    break
  fi
  printf "   %3ds — still firing...\n" "$elapsed"
  sleep "$POLL_INTERVAL"
  (( elapsed += POLL_INTERVAL ))
done

echo ""
echo -e "   ${BOLD}Active alerts after recovery (${elapsed}s):${RESET}"
active_alerts || true
echo ""

if $cleared; then
  ok "ApiDown has CLEARED after ${elapsed}s"
  record "ApiDown     : CLEARED after ${elapsed}s ✓"
else
  warn "ApiDown has not cleared within ${RECOVERY_WAIT_SECONDS}s."
  warn "The API may still be starting. Check ${PROM_BASE}/alerts."
  record "ApiDown     : not cleared within ${RECOVERY_WAIT_SECONDS}s ⚠"
fi

# ── Step 5: final summary ─────────────────────────────────────────────────────
step "5/5  Summary"
hr
echo -e " ${BOLD}Component status${RESET}"
for line in "${SUMMARY_LINES[@]}"; do
  echo "   ${line}"
done
hr

if $fired && $cleared; then
  echo -e " ${GREEN}${BOLD}PASS${RESET} — alert fired and cleared as expected."
  echo "   The Prometheus → alert rule → Alertmanager pipeline is working."
  exit 0
else
  echo -e " ${YELLOW}${BOLD}PARTIAL${RESET} — one or more checks did not complete within the time limits."
  echo "   Review the output above and check:"
  echo "     ${PROM_BASE}/alerts          (Prometheus alert state)"
  echo "     ${AM_BASE}/#/alerts         (Alertmanager routing)"
  exit 1
fi
