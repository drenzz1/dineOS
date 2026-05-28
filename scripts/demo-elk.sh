#!/usr/bin/env bash
# Demo: ELK centralized logging end-to-end.
# Proves the full log-aggregation pipeline:
#   Nginx → access.json → Filebeat → Logstash :5044 ──→ Elasticsearch
#   API   → Serilog TCPSink → Logstash :5001 ──────────→ Elasticsearch
#                                       ↓
#                                  Kibana dashboards
#
# Run from the repo root:  bash scripts/demo-elk.sh
#
# Prerequisites:
#   - Docker + Docker Compose
#   - .env with ES_PORT, KIBANA_PORT, Logstash__Uri (or use defaults)
set -euo pipefail

# ── Config ────────────────────────────────────────────────────────────────────
ES_PORT="${ES_PORT:-9200}"
KIBANA_PORT="${KIBANA_PORT:-5601}"
API_PORT="${API_HTTP_PORT:-5000}"
ES_BASE="http://localhost:${ES_PORT}"
KIBANA_BASE="http://localhost:${KIBANA_PORT}"
API_BASE="http://localhost:${API_PORT}"

REQUEST_COUNT=50
WAIT_ES_SECONDS=120       # ES single-node startup can be slow
WAIT_KIBANA_SECONDS=120    # Kibana needs ES healthy + bootstrap
WAIT_INDEX_SECONDS=60      # time for Filebeat → Logstash → ES indexing
POLL_INTERVAL=3

# Endpoints to hit — mix a public endpoint and one that exercises the full
# middleware pipeline (auth will fail but still generates structured logs).
ENDPOINTS=(
  "/api/v1/health"
  "/api/v1/menu/items"
)

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

# wait_for_health URL label timeout_seconds
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

# es_count INDEX_PATTERN — returns the document count (integer) from _count API
es_count() {
  local pattern="$1"
  local result
  result=$(curl -sf --max-time 5 "${ES_BASE}/${pattern}/_count" 2>/dev/null)
  if [[ -z "$result" ]]; then
    echo "0"
    return
  fi
  # Extract "count":<number> with a simple grep+sed rather than requiring jq
  echo "$result" | grep -o '"count":[0-9]*' | head -1 | cut -d: -f2
}

# ── Track results for final summary ──────────────────────────────────────────
SUMMARY_LINES=()
record() { SUMMARY_LINES+=("$1"); }

# Counter for passed/failed checks
PASSES=0
FAILURES=0
check() {
  local desc="$1" cond="$2"
  if eval "$cond"; then
    ok "$desc"
    (( PASSES += 1 ))
  else
    fail "$desc"
    (( FAILURES += 1 ))
  fi
}

# ── Main ──────────────────────────────────────────────────────────────────────
hr
echo -e " ${BOLD}DineOS — ELK logging demo${RESET}"
echo    " Elasticsearch : ${ES_BASE}"
echo    " Kibana        : ${KIBANA_BASE}"
echo    " API           : ${API_BASE}"
echo    " Requests      : ${REQUEST_COUNT}"
hr

# ── Step 1: start the ELK profile ─────────────────────────────────────────────
step "1/6  Start ELK profile (docker compose --profile elk up -d)"

if docker compose --profile elk ps 2>/dev/null | grep -q "elk"; then
  warn "ELK containers already running — skipping start"
else
  echo "   Starting ELK containers..."
  docker compose --profile elk up -d
fi

record "ELK stack   : STARTED"

# ── Step 2: wait for Elasticsearch green ──────────────────────────────────────
step "2/6  Wait for Elasticsearch cluster health green"

if wait_for_health "${ES_BASE}/_cluster/health?wait_for_status=yellow&timeout=5s" \
    "Elasticsearch" "${WAIT_ES_SECONDS}"; then
  :
else
  exit 1
fi

# Verify status is green (not yellow)
es_status=$(curl -sf --max-time 5 "${ES_BASE}/_cluster/health" \
  | grep -o '"status":"[^"]*"' | cut -d'"' -f4)
echo "   Cluster status: ${es_status}"
if [[ "$es_status" == "green" ]]; then
  ok "Cluster is green"
  record "ES cluster  : green"
else
  warn "Cluster is ${es_status} — single-node with no replicas should be green after templates"
  record "ES cluster  : ${es_status}"
fi

# ── Step 3: bootstrap ES + Kibana assets ─────────────────────────────────────
step "3/6  Bootstrap ILM policy, index templates, aliases, Kibana saved objects"

bash backend/elk/setup/bootstrap.sh

record "Bootstrap   : complete"

# Verify Kibana is ready after bootstrap (bootstrap already waits, but double-check)
if wait_for_health "${KIBANA_BASE}/api/status" \
    "Kibana" "${WAIT_KIBANA_SECONDS}"; then
  :
else
  exit 1
fi

record "Kibana      : ready"

# ── Step 4: issue 50 sample requests ──────────────────────────────────────────
step "4/6  Issue ${REQUEST_COUNT} sample requests"

# Record pre-request counts for later comparison
PRE_API_COUNT="$(es_count "dineos-api-logs-*")"
PRE_NGINX_COUNT="$(es_count "dineos-nginx-access-*")"
echo "   Pre-request counts: api=${PRE_API_COUNT}  nginx=${PRE_NGINX_COUNT}"

echo "   Sending ${REQUEST_COUNT} requests (alternating endpoints)..."
for i in $(seq 1 "$REQUEST_COUNT"); do
  endpoint="${ENDPOINTS[$(( (i - 1) % ${#ENDPOINTS[@]} ))]}"
  curl -sf --max-time 10 -o /dev/null "${API_BASE}${endpoint}" 2>/dev/null || true
  # Print a progress indicator every 10 requests
  if (( i % 10 == 0 )); then
    printf "   %3d / %d\n" "$i" "$REQUEST_COUNT"
  fi
done
ok "${REQUEST_COUNT} requests sent"

record "Requests    : ${REQUEST_COUNT}"

# ── Step 5: wait until hits appear in both indices ────────────────────────────
step "5/6  Wait for log documents to appear in Elasticsearch"

echo "   Waiting up to ${WAIT_INDEX_SECONDS}s for documents in both indices..."
elapsed=0
api_ok=false
nginx_ok=false

while (( elapsed < WAIT_INDEX_SECONDS )); do
  if ! $api_ok; then
    api_count="$(es_count "dineos-api-logs-*")"
    if [[ -n "$api_count" && "$api_count" != "0" && "$api_count" -gt "$PRE_API_COUNT" ]]; then
      api_ok=true
      ok "dineos-api-logs-* : ${api_count} documents (was ${PRE_API_COUNT})"
    fi
  fi

  if ! $nginx_ok; then
    nginx_count="$(es_count "dineos-nginx-access-*")"
    if [[ -n "$nginx_count" && "$nginx_count" != "0" && "$nginx_count" -gt "$PRE_NGINX_COUNT" ]]; then
      nginx_ok=true
      ok "dineos-nginx-access-* : ${nginx_count} documents (was ${PRE_NGINX_COUNT})"
    fi
  fi

  if $api_ok && $nginx_ok; then
    break
  fi

  printf "   %3ds — api=%s  nginx=%s\n" "$elapsed" \
    "$(es_count "dineos-api-logs-*")" "$(es_count "dineos-nginx-access-*")"
  sleep "$POLL_INTERVAL"
  (( elapsed += POLL_INTERVAL ))
done

echo ""
if ! $api_ok; then
  fail "No new documents in dineos-api-logs-* within ${WAIT_INDEX_SECONDS}s"
  record "API logs    : MISSING ✗"
else
  record "API logs    : ${api_count} docs ✓"
fi

if ! $nginx_ok; then
  fail "No new documents in dineos-nginx-access-* within ${WAIT_INDEX_SECONDS}s"
  record "Nginx logs  : MISSING ✗"
else
  record "Nginx logs  : ${nginx_count} docs ✓"
fi

# ── Step 6: verify correlation — check one doc from each index ────────────────
step "6/6  Verify field presence and correlation"

# Sample the most recent document from each index
api_doc=$(curl -sf --max-time 5 \
  "${ES_BASE}/dineos-api-logs-*/_search?size=1&sort=@timestamp:desc" \
  2>/dev/null)

nginx_doc=$(curl -sf --max-time 5 \
  "${ES_BASE}/dineos-nginx-access-*/_search?size=1&sort=@timestamp:desc" \
  2>/dev/null)

# Check API log fields
if echo "$api_doc" | grep -q '"CorrelationId"'; then
  ok "API doc contains CorrelationId"
else
  fail "API doc missing CorrelationId"
fi

if echo "$api_doc" | grep -q '"RequestPath"'; then
  ok "API doc contains RequestPath"
else
  fail "API doc missing RequestPath"
fi

if echo "$api_doc" | grep -q '"StatusCode"'; then
  ok "API doc contains StatusCode"
else
  fail "API doc missing StatusCode"
fi

if echo "$api_doc" | grep -q '"Elapsed"'; then
  ok "API doc contains Elapsed"
else
  fail "API doc missing Elapsed"
fi

# Check Nginx log fields
if echo "$nginx_doc" | grep -q '"status"'; then
  ok "Nginx doc contains status"
else
  fail "Nginx doc missing status"
fi

if echo "$nginx_doc" | grep -q '"request_uri"'; then
  ok "Nginx doc contains request_uri"
else
  fail "Nginx doc missing request_uri"
fi

if echo "$nginx_doc" | grep -q '"request_method"'; then
  ok "Nginx doc contains request_method"
else
  fail "Nginx doc missing request_method"
fi

if echo "$nginx_doc" | grep -q '"request_time"'; then
  ok "Nginx doc contains request_time"
else
  fail "Nginx doc missing request_time"
fi

if echo "$nginx_doc" | grep -q '"remote_addr"'; then
  ok "Nginx doc contains remote_addr"
else
  fail "Nginx doc missing remote_addr"
fi

# Check Kibana saved objects are present
dashboard_count=$(curl -sf --max-time 5 \
  "${KIBANA_BASE}/api/saved_objects/_find?type=dashboard&per_page=10" \
  -H "kbn-xsrf: true" 2>/dev/null \
  | grep -o '"total":[0-9]*' | head -1 | cut -d: -f2)
if [[ -n "$dashboard_count" && "$dashboard_count" != "0" ]]; then
  ok "Kibana dashboards : ${dashboard_count} present"
else
  warn "Kibana dashboards : could not verify (Kibana may still be initialising)"
fi

record "Correlation : checked"
record "Dashboards  : ${dashboard_count:-?}"

# ── Final summary ─────────────────────────────────────────────────────────────
hr
echo -e " ${BOLD}ELK demo complete${RESET}"
for line in "${SUMMARY_LINES[@]}"; do
  echo "   ${line}"
done
hr

ALL_OK=true

if ! $api_ok; then
  echo -e " ${RED}${BOLD}FAIL${RESET} — no documents in dineos-api-logs-*"
  echo    "   Check: docker compose logs api | grep -i logstash"
  echo    "   Check: docker compose logs logstash --tail=50"
  ALL_OK=false
fi

if ! $nginx_ok; then
  echo -e " ${RED}${BOLD}FAIL${RESET} — no documents in dineos-nginx-access-*"
  echo    "   Check: docker compose logs filebeat --tail=50"
  echo    "   Check: docker compose logs logstash --tail=50"
  echo    "   Check: docker compose exec nginx tail -5 /var/log/nginx/access.json"
  ALL_OK=false
fi

if $ALL_OK; then
  echo -e " ${GREEN}${BOLD}PASS${RESET} — both indices received log documents."
  echo    "   Open ${KIBANA_BASE} → Analytics → Dashboards to explore."
  echo    "   Cleanup: docker compose --profile elk down -v"
  exit 0
else
  exit 1
fi
