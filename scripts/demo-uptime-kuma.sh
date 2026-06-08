#!/usr/bin/env bash
# Demo: Uptime Kuma DOWN detection and recovery notification.
# Proves the full synthetic-monitoring pipeline end-to-end:
#   Kuma poll → DOWN detected → SMTP email → Mailhog → restart → recovery email
#
# Run from the repo root:  bash scripts/demo-uptime-kuma.sh
#
# Prerequisites:
#   - docker compose up -d && docker compose --profile uptime up -d
#   - bootstrap + backup import already run (backend/uptime-kuma/setup/bootstrap.sh)
#   - SMTP notification channel wired to mailhog:1025 (default after backup import)
#   - Optional: UPTIME_KUMA_PORT / MAILHOG_UI_PORT if not using defaults 3001/8025
set -euo pipefail

# ── Config ────────────────────────────────────────────────────────────────────
KUMA_PORT="${UPTIME_KUMA_PORT:-3001}"
MAILHOG_PORT="${MAILHOG_UI_PORT:-8025}"
KUMA_BASE="http://localhost:${KUMA_PORT}"
MAILHOG_BASE="http://localhost:${MAILHOG_PORT}"
KUMA_USER="${KUMA_ADMIN_USER:-admin}"
KUMA_PASS="${KUMA_ADMIN_PASSWORD:-admin}"
MONITOR_NAME="API – Liveness"

# Kuma polls every 30 s with 3 retries → worst-case DOWN detection = 90 s.
# Add 60 s buffer for email delivery and processing.
DOWN_WAIT_SECONDS=150
RECOVERY_WAIT_SECONDS=90
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

curl_health() {
  curl -sf --max-time 5 "$1" > /dev/null 2>&1
}

# mailhog_total — total number of messages currently in Mailhog
mailhog_total() {
  curl -sf --max-time 5 "${MAILHOG_BASE}/api/v2/messages?limit=1" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{const d=JSON.parse(Buffer.concat(c).toString());process.stdout.write(String(d.total||0));}
  catch(e){process.stdout.write('0');}
});" 2>/dev/null || echo "0"
}

# mailhog_subjects_since COUNT — subjects of messages newer than COUNT total
mailhog_subjects_since() {
  local since="$1"
  curl -sf --max-time 5 "${MAILHOG_BASE}/api/v2/messages?limit=20" \
    | node -e "
const since=${since};
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try {
    const d=JSON.parse(Buffer.concat(c).toString());
    // items are newest-first; take only those that arrived after our baseline
    const items=(d.items||[]).slice(0, Math.max(0, (d.total||0) - since));
    items.forEach(m=>{
      const subj=((m.Content&&m.Content.Headers&&m.Content.Headers.Subject)||['(no subject)'])[0];
      console.log('     ' + subj);
    });
  } catch(e){}
});" 2>/dev/null
}

# kuma_monitor_status MONITOR_ID TOKEN — prints "UP", "DOWN", or "UNKNOWN"
kuma_monitor_status() {
  local id="$1" token="$2"
  curl -sf --max-time 5 \
    -H "Authorization: Bearer ${token}" \
    "${KUMA_BASE}/api/v1/monitors/${id}/beats?limit=1" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try {
    const d=JSON.parse(Buffer.concat(c).toString());
    const beats=(d.data||[]);
    if(beats.length>0){process.stdout.write(beats[0].status===1?'UP':'DOWN');}
    else{process.stdout.write('UNKNOWN');}
  } catch(e){process.stdout.write('UNKNOWN');}
});" 2>/dev/null || echo "UNKNOWN"
}

# ── Track results for final summary ──────────────────────────────────────────
SUMMARY_LINES=()
record() { SUMMARY_LINES+=("$1"); }

# ── Main ──────────────────────────────────────────────────────────────────────
hr
echo -e " ${BOLD}DineOS — Uptime Kuma DOWN/UP demo${RESET}"
echo    " Kuma   : ${KUMA_BASE}"
echo    " Mailhog: ${MAILHOG_BASE}"
hr

# ── Step 1: verify services are reachable ────────────────────────────────────
step "1/6  Verify Kuma and Mailhog are reachable"

if ! curl_health "${KUMA_BASE}/api/entry-page"; then
  fail "Uptime Kuma is not reachable at ${KUMA_BASE}/api/entry-page"
  echo "      Start the stack:  docker compose --profile uptime up -d" >&2
  exit 1
fi
ok "Uptime Kuma ${KUMA_BASE}/api/entry-page → up"

if ! curl_health "${MAILHOG_BASE}/api/v2/messages"; then
  fail "Mailhog is not reachable at ${MAILHOG_BASE}/api/v2/messages"
  echo "      Start the stack:  docker compose up -d" >&2
  exit 1
fi
ok "Mailhog     ${MAILHOG_BASE}/api/v2/messages → up"

record "Kuma          : UP"
record "Mailhog       : UP"

# ── Step 2: authenticate and locate the target monitor ───────────────────────
step "2/6  Authenticate with Kuma"

token_resp=$(curl -sf --max-time 5 \
  -X POST "${KUMA_BASE}/login/access-token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=${KUMA_USER}&password=${KUMA_PASS}&token=" 2>/dev/null || true)
TOKEN=$(echo "${token_resp}" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)

if [[ -z "${TOKEN}" ]]; then
  fail "Could not obtain Kuma auth token — check KUMA_ADMIN_USER / KUMA_ADMIN_PASSWORD"
  exit 1
fi
ok "Token obtained for '${KUMA_USER}'"

# Find the monitor ID for MONITOR_NAME
MONITOR_ID=$(curl -sf --max-time 5 \
  -H "Authorization: Bearer ${TOKEN}" \
  "${KUMA_BASE}/api/v1/monitors" \
  | node -e "
const name='${MONITOR_NAME}';
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{
    const d=JSON.parse(Buffer.concat(c).toString());
    const monitors=d.monitors||d||[];
    const m=monitors.find(m=>m.name===name);
    process.stdout.write(m?String(m.id):'');
  }catch(e){process.stdout.write('');}
});" 2>/dev/null || true)

if [[ -z "${MONITOR_ID}" ]]; then
  warn "Monitor '${MONITOR_NAME}' not found via REST API."
  warn "Run the bootstrap + backup import first (see backend/uptime-kuma/README.md)."
  warn "Continuing — PASS verdict will rely on Mailhog notification count only."
  MONITOR_ID="0"
else
  ok "Found monitor '${MONITOR_NAME}' (id ${MONITOR_ID})"
fi

# ── Step 3: baseline — confirm API is running and monitor is UP ───────────────
step "3/6  Baseline — confirm API is UP"

if ! docker compose ps --status running api 2>/dev/null | grep -q "api"; then
  warn "The 'api' service is not running — starting it for a clean baseline."
  docker compose start api
  echo "   Waiting 30 s for API to boot..."
  sleep 30
fi
ok "API container is running"

if [[ "${MONITOR_ID}" != "0" ]]; then
  cur_status=$(kuma_monitor_status "${MONITOR_ID}" "${TOKEN}")
  ok "Kuma: ${MONITOR_NAME} = ${cur_status}"
fi

BASELINE=$(mailhog_total)
info_line="   →  Mailhog baseline: ${BASELINE} message(s)"
echo -e "   ${info_line}"

record "API pre-state : RUNNING"

# ── Step 4: stop API, wait for DOWN notification ─────────────────────────────
step "4/6  Stop API → wait ${DOWN_WAIT_SECONDS}s for DOWN notification in Mailhog"
echo    "   Kuma polls '${MONITOR_NAME}' every 30 s with 3 retries → worst-case 90 s"

docker compose stop api
ok "docker compose stop api — done"

echo "   Polling Mailhog for new message (every ${POLL_INTERVAL}s, timeout ${DOWN_WAIT_SECONDS}s)..."
elapsed=0
down_fired=false
while (( elapsed < DOWN_WAIT_SECONDS )); do
  current=$(mailhog_total)
  if (( current > BASELINE )); then
    down_fired=true
    DOWN_MSG_COUNT="${current}"
    break
  fi
  printf "   %3ds — waiting for Kuma DOWN notification...\n" "$elapsed"
  sleep "$POLL_INTERVAL"
  (( elapsed += POLL_INTERVAL ))
done

echo ""
if $down_fired; then
  ok "DOWN notification received after ${elapsed}s"
  record "DOWN alert    : fired after ${elapsed}s ✓"
  echo -e "   ${BOLD}New Mailhog message(s):${RESET}"
  mailhog_subjects_since "${BASELINE}" || true
  if [[ "${MONITOR_ID}" != "0" ]]; then
    cur_status=$(kuma_monitor_status "${MONITOR_ID}" "${TOKEN}")
    echo -e "   →  Kuma: ${MONITOR_NAME} = ${cur_status}"
  fi
else
  DOWN_MSG_COUNT="${BASELINE}"
  warn "No new Mailhog message arrived within ${DOWN_WAIT_SECONDS}s."
  warn "Check that:"
  warn "  - The SMTP notification channel is configured (backend/uptime-kuma/README.md)"
  warn "  - The backup import was run (Settings → Backup → Import)"
  warn "  - Kuma is polling: open ${KUMA_BASE} and check the '${MONITOR_NAME}' monitor"
  record "DOWN alert    : did not arrive within ${DOWN_WAIT_SECONDS}s ⚠"
fi

# ── Step 5: restart API, wait for recovery notification ──────────────────────
step "5/6  Restart API → wait ${RECOVERY_WAIT_SECONDS}s for recovery notification"

docker compose start api
ok "docker compose start api — done"

echo "   Polling Mailhog for recovery message (every ${POLL_INTERVAL}s, timeout ${RECOVERY_WAIT_SECONDS}s)..."
elapsed=0
recovered=false
while (( elapsed < RECOVERY_WAIT_SECONDS )); do
  current=$(mailhog_total)
  if (( current > DOWN_MSG_COUNT )); then
    recovered=true
    break
  fi
  printf "   %3ds — waiting for recovery notification...\n" "$elapsed"
  sleep "$POLL_INTERVAL"
  (( elapsed += POLL_INTERVAL ))
done

echo ""
if $recovered; then
  ok "Recovery notification received after ${elapsed}s"
  record "Recovery alert: fired after ${elapsed}s ✓"
  echo -e "   ${BOLD}New Mailhog message(s):${RESET}"
  mailhog_subjects_since "${DOWN_MSG_COUNT}" || true
  if [[ "${MONITOR_ID}" != "0" ]]; then
    cur_status=$(kuma_monitor_status "${MONITOR_ID}" "${TOKEN}")
    echo -e "   →  Kuma: ${MONITOR_NAME} = ${cur_status}"
  fi
else
  warn "Recovery notification did not arrive within ${RECOVERY_WAIT_SECONDS}s."
  warn "The API may still be starting. Check ${KUMA_BASE} for current monitor state."
  record "Recovery alert: did not arrive within ${RECOVERY_WAIT_SECONDS}s ⚠"
fi

# ── Step 6: final summary ─────────────────────────────────────────────────────
step "6/6  Summary"
hr
echo -e " ${BOLD}Component status${RESET}"
for line in "${SUMMARY_LINES[@]}"; do
  echo "   ${line}"
done
hr

if $down_fired && $recovered; then
  echo -e " ${GREEN}${BOLD}PASS${RESET} — DOWN and recovery notifications both arrived."
  echo    "   The Uptime Kuma → SMTP → Mailhog pipeline is working."
  echo    "   Open ${MAILHOG_BASE} to see the full email bodies."
  exit 0
else
  echo -e " ${YELLOW}${BOLD}PARTIAL${RESET} — one or more notifications did not arrive within the time limits."
  echo    "   Review the output above and check:"
  echo    "     ${KUMA_BASE}                  (Kuma dashboard — monitor states)"
  echo    "     ${KUMA_BASE}/status/dineos    (Public status page)"
  echo    "     ${MAILHOG_BASE}               (Email inbox)"
  exit 1
fi
