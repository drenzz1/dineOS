#!/usr/bin/env bash
# Demo: DO-12 AI-powered incident triage pipeline end to end.
# Proves the full path:
#   POST payload → /api/v1/alerts/webhook → LLM triage → Slack notification
#
# Two modes:
#   FAST (default)   — POSTs a sample Alertmanager payload directly to the
#                      webhook endpoint.  Completes in seconds; no need to
#                      wait for Prometheus alert rules to fire.
#   REAL_ALERT=true  — Stops the API container, waits for ApiDown to fire
#                      via Alertmanager → webhook, then restarts.  Takes ~2 min
#                      but exercises the full Prometheus → Alertmanager path.
#
# Run from the repo root:  bash scripts/demo-do12.sh
#
# Environment variables:
#   API_HTTP_PORT   — local API port         (default: 5001)
#   ALERTMANAGER_PORT — local AM port        (default: 9093)
#   API_BASE        — override base URL entirely (e.g. https://app.project-06.gjirafa.dev/api)
#                     When set, docker log polling is skipped automatically.
#   ALERT_WEBHOOK_SECRET — shared secret header (leave empty for local dev)
#   REAL_ALERT      — set to "true" to use the slow Prometheus → AM path
set -euo pipefail

# ── Config ────────────────────────────────────────────────────────────────────
API_HTTP_PORT="${API_HTTP_PORT:-5001}"
AM_PORT="${ALERTMANAGER_PORT:-9093}"

# API_BASE can be overridden to point at any environment.
# For live: API_BASE=https://app.project-06.gjirafa.dev/api bash scripts/demo-do12.sh
if [[ -z "${API_BASE:-}" ]]; then
  API_BASE="http://localhost:${API_HTTP_PORT}/api"
  LOCAL_DOCKER=true
else
  LOCAL_DOCKER=false
fi

AM_BASE="http://localhost:${AM_PORT}"
WEBHOOK_SECRET="${ALERT_WEBHOOK_SECRET:-}"

POLL_INTERVAL=3
LOG_POLL_TIMEOUT=30    # seconds to wait for correlation ID to appear in logs

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

# ── Helpers ───────────────────────────────────────────────────────────────────

curl_health() {
  curl -sf --max-time 5 "$1" > /dev/null 2>&1
}

# json_field JSON field — extract a top-level string field with node, or grep fallback
json_field() {
  local json="$1" field="$2"
  echo "$json" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{const o=JSON.parse(Buffer.concat(c).toString());
      process.stdout.write(String(o['${field}']??''));}
  catch(e){}
});" 2>/dev/null \
    || echo "$json" | grep -o "\"${field}\":\"[^\"]*\"" | head -1 | cut -d'"' -f4 || true
}

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

# extract_correlation_ids JSON — prints each correlationId on its own line
extract_correlation_ids() {
  echo "$1" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{
    const o=JSON.parse(Buffer.concat(c).toString());
    const arr=(o.data||[]);
    for(const r of arr){if(r.correlationId)console.log(r.correlationId);}
  }catch(e){}
});" 2>/dev/null \
    || grep -o '"correlationId":"[^"]*"' <<< "$1" | cut -d'"' -f4 || true
}

# post_webhook PAYLOAD — POSTs to the webhook endpoint, returns response body
post_webhook() {
  local payload="$1"
  local extra_args=()
  if [[ -n "$WEBHOOK_SECRET" ]]; then
    extra_args+=(-H "X-Webhook-Secret: ${WEBHOOK_SECRET}")
  fi
  curl -sf --max-time 15 \
    -X POST \
    -H "Content-Type: application/json" \
    "${extra_args[@]}" \
    -d "$payload" \
    "${API_BASE}/v1/alerts/webhook" 2>&1 || true
}

# poll_logs_for CORRELATION_ID — waits for the ID to appear in docker logs
poll_logs_for() {
  local corr_id="$1"
  local elapsed=0
  printf "   Waiting for CorrelationId=%s in logs" "$corr_id"
  while (( elapsed < LOG_POLL_TIMEOUT )); do
    if docker compose logs api --tail 200 2>/dev/null | grep -q "$corr_id"; then
      echo ""
      return 0
    fi
    printf "."
    sleep "$POLL_INTERVAL"
    (( elapsed += POLL_INTERVAL ))
  done
  echo ""
  return 1
}

# ── Track results for summary ─────────────────────────────────────────────────
SUMMARY_LINES=()
record() { SUMMARY_LINES+=("$1"); }
PASS=true

# ── Sample Alertmanager payload ───────────────────────────────────────────────
SAMPLE_PAYLOAD='{
  "version": "4",
  "groupKey": "{}:{alertname=\"HighErrorRate\"}",
  "status": "firing",
  "receiver": "dineos-webhook",
  "groupLabels":  { "alertname": "HighErrorRate" },
  "commonLabels": { "alertname": "HighErrorRate", "severity": "critical", "component": "api", "env": "demo" },
  "commonAnnotations": {
    "summary":     "HTTP 5xx error rate above 10% for 5 minutes",
    "description": "5xx responses spiking on /api/orders — likely upstream DB connection exhaustion"
  },
  "externalURL": "http://alertmanager:9093",
  "alerts": [
    {
      "status": "firing",
      "labels": {
        "alertname": "HighErrorRate",
        "severity":  "critical",
        "component": "api",
        "job":       "dineos-api",
        "env":       "demo"
      },
      "annotations": {
        "summary":     "HTTP 5xx error rate above 10% for 5 minutes",
        "description": "5xx responses spiking on /api/orders — likely upstream DB connection exhaustion"
      },
      "startsAt":    "'"$(date -u +"%Y-%m-%dT%H:%M:%SZ")"'",
      "endsAt":      "0001-01-01T00:00:00Z",
      "generatorURL": "http://prometheus:9090/graph",
      "fingerprint":  "demo'"$(date +%s)"'"
    }
  ]
}'

# ── Main ──────────────────────────────────────────────────────────────────────
hr
echo -e " ${BOLD}DineOS — DO-12 AI incident triage demo${RESET}"
echo    " API base    : ${API_BASE}"
if $LOCAL_DOCKER; then
  echo  " Alertmanager: ${AM_BASE}"
fi
echo    " Mode        : ${REAL_ALERT:-fast} (REAL_ALERT=true for full Prometheus path)"
hr

# ── Step 1: verify the API is reachable ──────────────────────────────────────
step "1/4  Verify API health endpoint"

HEALTH_URL="${API_BASE}/v1/health"
if ! curl_health "$HEALTH_URL"; then
  fail "API not reachable at ${HEALTH_URL}"
  if $LOCAL_DOCKER; then
    info "Start the stack:  docker compose up -d"
  fi
  exit 1
fi
ok "API ${HEALTH_URL} → healthy"
record "API health  : UP"

if $LOCAL_DOCKER && ! curl_health "${AM_BASE}/-/ready"; then
  warn "Alertmanager not reachable at ${AM_BASE}/-/ready"
  warn "Real-alert mode will be skipped.  Start with:  docker compose up -d"
  record "Alertmanager: NOT reachable (fast mode only)"
else
  if $LOCAL_DOCKER; then
    ok "Alertmanager ${AM_BASE}/-/ready → healthy"
    record "Alertmanager: UP"
  fi
fi

# ── Step 2: POST the sample Alertmanager payload ─────────────────────────────
step "2/4  POST sample Alertmanager payload to ${API_BASE}/v1/alerts/webhook"

if [[ -n "$WEBHOOK_SECRET" ]]; then
  info "Using X-Webhook-Secret header (ALERT_WEBHOOK_SECRET is set)"
else
  info "No shared secret configured — unauthenticated call (expected for local dev)"
fi
info ""

POST_TS=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
RESPONSE=$(post_webhook "$SAMPLE_PAYLOAD")

if [[ -z "$RESPONSE" ]]; then
  fail "No response from webhook endpoint (curl returned empty or error)"
  PASS=false
  record "Webhook POST: NO RESPONSE ✗"
else
  ok "Webhook responded"
  echo ""
  echo -e "   ${BOLD}Response body:${RESET}"
  pretty_json "$RESPONSE" | sed 's/^/   /'
  echo ""

  SUCCESS=$(json_field "$RESPONSE" "success")
  if [[ "$SUCCESS" == "true" ]]; then
    ok "success=true — alert pipeline acknowledged"
    record "Webhook POST: 200 success=true ✓"
  else
    warn "success field not true — response may indicate an error"
    record "Webhook POST: unexpected response ⚠"
    PASS=false
  fi
fi

# ── Step 3: inspect triage results and logs ──────────────────────────────────
step "3/4  Check triage results and backend logs"

CORR_IDS=$(extract_correlation_ids "$RESPONSE")

if [[ -z "$CORR_IDS" ]]; then
  warn "No triage results returned (empty data array)."
  warn "This is expected when no AI provider API key is configured."
  warn "Set Anthropic__ApiKey (or OpenAI__ApiKey / GoogleAI__ApiKey) in .env to enable triage."
  record "LLM triage  : skipped — no AI key configured ⚠"
else
  echo ""
  echo -e "   ${BOLD}Triage results:${RESET}"
  echo "$RESPONSE" \
    | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{
    const o=JSON.parse(Buffer.concat(c).toString());
    const arr=(o.data||[]);
    for(const r of arr){
      console.log('   ─────────────────────────────────────────');
      console.log('   CorrelationId : ' + r.correlationId);
      console.log('   AlertName     : ' + r.alertName);
      console.log('   Severity      : ' + r.severity);
      console.log('   Short Summary : ' + r.shortSummary);
      console.log('   Likely Causes :');
      (r.likelyCauses||[]).forEach((c,i)=>console.log('     '+(i+1)+'. '+c));
      console.log('   Next Actions  :');
      (r.suggestedNextActions||[]).forEach((a,i)=>console.log('     '+(i+1)+'. '+a));
      if(r.usage) console.log('   AI Model      : '+r.usage.model+' (in:'+r.usage.inputTokens+' out:'+r.usage.outputTokens+')');
    }
    console.log('   ─────────────────────────────────────────');
  }catch(e){console.log('   (could not parse results)');}
});" 2>/dev/null || true
  echo ""
  ok "$(echo "$CORR_IDS" | wc -l | tr -d ' ') triage result(s) returned"
  record "LLM triage  : $(echo "$CORR_IDS" | wc -l | tr -d ' ') result(s) ✓"
fi

# Log inspection — only when running locally with docker compose
if $LOCAL_DOCKER; then
  echo ""
  if [[ -z "$CORR_IDS" ]]; then
    echo -e "   ${BOLD}Recent API logs (last 30 lines):${RESET}"
    docker compose logs api --tail 30 2>/dev/null | grep -E "webhook|triage|Slack|alert" | sed 's/^/   /' || true
    record "Log check   : AI key not configured — triage logs absent"
  else
    FIRST_CORR=$(echo "$CORR_IDS" | head -1)
    echo -e "   ${BOLD}Searching logs for CorrelationId=${FIRST_CORR}${RESET}"

    if poll_logs_for "$FIRST_CORR"; then
      ok "CorrelationId found in logs"

      echo ""
      echo -e "   ${BOLD}Relevant log lines:${RESET}"
      docker compose logs api --tail 300 2>/dev/null \
        | grep -E "$FIRST_CORR|webhook|triage|Slack" \
        | tail -20 \
        | sed 's/^/   /' || true
      echo ""

      # Check for Slack outcome
      if docker compose logs api --tail 300 2>/dev/null | grep -q "Slack notification sent"; then
        ok "Slack notification sent"
        record "Slack post  : sent ✓"
      elif docker compose logs api --tail 300 2>/dev/null | grep -q "Slack WebhookUrl is not configured"; then
        warn "Slack no-op: SLACK_WEBHOOK_URL not configured in .env"
        info "Set SLACK_WEBHOOK_URL in .env to enable Slack notifications."
        record "Slack post  : skipped — SLACK_WEBHOOK_URL not set ⚠"
      elif docker compose logs api --tail 300 2>/dev/null | grep -q "Slack notification failed"; then
        warn "Slack notification attempted but failed (check webhook URL)"
        record "Slack post  : HTTP error from Slack ⚠"
      else
        info "Slack outcome not yet visible in recent logs."
        record "Slack post  : outcome unknown (check logs manually)"
      fi

      record "Log check   : CorrelationId found ✓"
    else
      warn "CorrelationId not found in logs within ${LOG_POLL_TIMEOUT}s."
      warn "The triage may have completed; check manually:"
      info "  docker compose logs api --tail 100 | grep triage"
      record "Log check   : correlation ID not found within ${LOG_POLL_TIMEOUT}s ⚠"
      PASS=false
    fi
  fi
else
  info "Running against remote API — docker log polling skipped."
  info "Check your logging stack (Grafana/Loki or Kibana) and filter for:"
  if [[ -n "$CORR_IDS" ]]; then
    echo "$CORR_IDS" | while read -r id; do
      info "  CorrelationId=$id"
    done
  else
    info "  'Incident triage' OR 'webhook received'"
  fi
  record "Log check   : remote env — manual log inspection required"
fi

# ── Step 4: optional real-alert path ─────────────────────────────────────────
if [[ "${REAL_ALERT:-false}" == "true" ]]; then
  step "3b  REAL_ALERT=true — firing ApiDown via Prometheus → Alertmanager path"
  if ! $LOCAL_DOCKER; then
    warn "REAL_ALERT mode requires docker compose (local only). Skipping."
  else
    info "This mirrors demo-alert.sh: stops the API, waits for ApiDown to fire,"
    info "which Alertmanager routes to the backend webhook instead of Slack directly."
    info ""

    STOP_WAIT=120
    POLL_INT=5

    docker compose stop api
    ok "API container stopped"

    echo "   Polling for ApiDown to fire (Prometheus → rule → Alertmanager → webhook)..."
    elapsed=0
    fired=false
    PROM_PORT="${PROMETHEUS_PORT:-9090}"
    PROM_BASE="http://localhost:${PROM_PORT}"
    while (( elapsed < STOP_WAIT )); do
      if curl -sf --max-time 5 "${PROM_BASE}/api/v1/alerts" 2>/dev/null \
          | node -e "
const c=[];process.stdin.on('data',d=>c.push(d));process.stdin.on('end',()=>{
  try{const d=JSON.parse(Buffer.concat(c).toString());
      const alerts=(d.data||{}).alerts||[];
      for(const a of alerts){
        if((a.labels||{}).alertname==='ApiDown'&&a.state==='firing')process.exit(0);
      }
  }catch(e){}process.exit(1);});" 2>/dev/null; then
        fired=true
        break
      fi
      printf "   %3ds — waiting for ApiDown...\n" "$elapsed"
      sleep "$POLL_INT"
      (( elapsed += POLL_INT ))
    done

    if $fired; then
      ok "ApiDown FIRING — Alertmanager will route to dineos-webhook"
      record "Real alert  : ApiDown fired ✓"
      info "Wait 10s for Alertmanager to deliver to the webhook..."
      sleep 10
    else
      warn "ApiDown did not fire within ${STOP_WAIT}s. Check ${PROM_BASE}/alerts."
      record "Real alert  : did not fire ⚠"
    fi

    docker compose start api
    ok "API container restarted"
  fi
fi

# ── Step 4: final summary ─────────────────────────────────────────────────────
step "4/4  Summary"
hr
echo -e " ${BOLD}Component status${RESET}"
for line in "${SUMMARY_LINES[@]}"; do
  echo "   ${line}"
done
hr

if $PASS; then
  echo -e " ${GREEN}${BOLD}PASS${RESET} — DO-12 triage pipeline is operational."
  echo    "   Alerts reach the backend, are processed (or skipped if no AI key),"
  echo    "   and Slack is notified (or skipped if SLACK_WEBHOOK_URL not set)."
  echo    "   The alert pipeline is never blocked — the endpoint always returns 200."
  echo ""
  echo    "   To enable full AI triage:"
  echo    "     Set Anthropic__ApiKey (or OpenAI__ApiKey / GoogleAI__ApiKey) in .env"
  echo    "   To enable Slack notifications:"
  echo    "     Set SLACK_WEBHOOK_URL in .env"
  echo    "   Architecture docs: docs/devops/aiops-triage.md"
  exit 0
else
  echo -e " ${YELLOW}${BOLD}PARTIAL${RESET} — one or more checks did not complete."
  echo    "   Review the output above."
  echo    "     ${API_BASE}/v1/health    (API liveness)"
  if $LOCAL_DOCKER; then
    echo  "     docker compose logs api  (triage + Slack logs)"
  fi
  exit 1
fi
