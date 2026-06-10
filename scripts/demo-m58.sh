#!/usr/bin/env bash
# Demo: M5.8 n8n automation pipeline end to end.
# Proves the full path:
#   POST order JSON -> n8n /webhook/order-triage -> Anthropic LLM -> Slack + HTTP response
#
# The pipeline degrades gracefully: with no ANTHROPIC_API_KEY the webhook still
# returns 200 with an "AI summary unavailable" message, and Slack still receives
# a post (if SLACK_WEBHOOK_URL is set).
#
# Run from the repo root:  bash scripts/demo-m58.sh
#
# Environment variables:
#   N8N_PORT   — local n8n port              (default: 5678)
#   N8N_BASE   — override base URL entirely   (e.g. http://localhost:5678)
set -euo pipefail

# ── Config ────────────────────────────────────────────────────────────────────
N8N_PORT="${N8N_PORT:-5678}"
N8N_BASE="${N8N_BASE:-http://localhost:${N8N_PORT}}"
WEBHOOK_URL="${N8N_BASE}/webhook/order-triage"
HEALTH_URL="${N8N_BASE}/healthz"

# ── Colour codes ─────────────────────────────────────────────────────────────
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
info()  { echo    "   $*"; }

# pretty_json JSON — pretty print with node, or echo as-is
pretty_json() {
  echo "$1" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{process.stdout.write(JSON.stringify(JSON.parse(Buffer.concat(c).toString()),null,2)+'\n');}
  catch(e){process.stdout.write(Buffer.concat(c).toString());}
});" 2>/dev/null \
    || echo "$1"
}

# json_field JSON field — extract a top-level field with node
json_field() {
  echo "$1" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{const o=JSON.parse(Buffer.concat(c).toString());
      process.stdout.write(String(o['$2']??''));}
  catch(e){}
});" 2>/dev/null || true
}

SUMMARY_LINES=()
record() { SUMMARY_LINES+=("$1"); }
PASS=true

# ── Sample order payload ──────────────────────────────────────────────────────
SAMPLE_ORDER='{
  "orderId": "ORD-1042",
  "table": 7,
  "items": [
    { "name": "Ribeye steak", "qty": 2, "notes": "one well-done, nut allergy" },
    { "name": "Truffle fries", "qty": 1 },
    { "name": "House red", "qty": 1 }
  ],
  "total": 96.50,
  "currency": "EUR",
  "createdAt": "'"$(date -u +"%Y-%m-%dT%H:%M:%SZ")"'"
}'

# ── Main ──────────────────────────────────────────────────────────────────────
hr
echo -e " ${BOLD}DineOS — M5.8 n8n automation demo${RESET}"
echo    " n8n base    : ${N8N_BASE}"
echo    " Webhook     : ${WEBHOOK_URL}"
hr

# ── Step 1: verify n8n is reachable ───────────────────────────────────────────
step "1/3  Verify n8n health endpoint"
if ! curl -sf --max-time 5 "$HEALTH_URL" > /dev/null 2>&1; then
  fail "n8n not reachable at ${HEALTH_URL}"
  info "Start it:  docker compose up -d n8n"
  exit 1
fi
ok "n8n ${HEALTH_URL} → healthy"
record "n8n health  : UP"

# ── Step 2: POST the sample order to the webhook ──────────────────────────────
step "2/3  POST sample order to ${WEBHOOK_URL}"
RESPONSE=$(curl -sS --max-time 60 \
  -X POST \
  -H "Content-Type: application/json" \
  -d "$SAMPLE_ORDER" \
  "$WEBHOOK_URL" 2>&1 || true)

if [[ -z "$RESPONSE" ]]; then
  fail "No response from the webhook (is the workflow active? check: docker compose logs n8n)"
  PASS=false
  record "Webhook POST: NO RESPONSE ✗"
else
  ok "Webhook responded"
  echo ""
  echo -e "   ${BOLD}Response body:${RESET}"
  pretty_json "$RESPONSE" | sed 's/^/   /'
  echo ""
fi

# ── Step 3: inspect the summary ───────────────────────────────────────────────
step "3/3  Inspect LLM summary + Slack outcome"
SUMMARY="$(json_field "$RESPONSE" "summary")"
OK_FIELD="$(json_field "$RESPONSE" "ok")"

if [[ "$OK_FIELD" == "true" && -n "$SUMMARY" ]]; then
  if echo "$SUMMARY" | grep -qi "AI summary unavailable"; then
    warn "Pipeline ran, but LLM triage was skipped (no ANTHROPIC_API_KEY)."
    info "Set Anthropic__ApiKey in .env and recreate n8n to enable the LLM step:"
    info "  docker compose up -d --force-recreate n8n"
    record "LLM triage  : skipped — no AI key ⚠"
  else
    ok "LLM summary returned:"
    echo -e "   ${BOLD}${SUMMARY}${RESET}"
    record "LLM triage  : summary returned ✓"
  fi
else
  warn "Unexpected response shape — check the workflow in the n8n editor."
  record "Webhook POST: unexpected response ⚠"
  PASS=false
fi

if [[ "${SLACK_WEBHOOK_URL:-}" =~ ^https://hooks\.slack\.com/services/ ]] \
   && [[ ! "${SLACK_WEBHOOK_URL:-}" =~ placeholder ]]; then
  info "SLACK_WEBHOOK_URL is set — check your Slack channel for the triage post."
  record "Slack post  : attempted (verify in Slack) ✓"
else
  warn "SLACK_WEBHOOK_URL not set (or placeholder) — Slack post was a no-op."
  record "Slack post  : skipped — SLACK_WEBHOOK_URL not set ⚠"
fi

# ── Summary ───────────────────────────────────────────────────────────────────
step "Summary"
hr
echo -e " ${BOLD}Component status${RESET}"
for line in "${SUMMARY_LINES[@]}"; do
  echo "   ${line}"
done
hr

if $PASS; then
  echo -e " ${GREEN}${BOLD}PASS${RESET} — the M5.8 webhook → LLM → notification pipeline is operational."
  echo    "   To enable the LLM step:    set Anthropic__ApiKey in .env"
  echo    "   To enable Slack delivery:  set SLACK_WEBHOOK_URL in .env"
  echo    "   Then recreate n8n:         docker compose up -d --force-recreate n8n"
  echo    "   Architecture docs:         docs/devops/n8n-automation.md"
  exit 0
else
  echo -e " ${YELLOW}${BOLD}PARTIAL${RESET} — one or more checks did not complete."
  echo    "   Inspect:  docker compose logs n8n"
  echo    "   Editor :  ${N8N_BASE}  (workflow: dineOS Order Triage)"
  exit 1
fi
