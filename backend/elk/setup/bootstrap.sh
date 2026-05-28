#!/usr/bin/env bash
# =============================================================================
# DineOS ELK bootstrap — idempotent
#
# Registers the ILM policy, both index templates, and the initial write-alias
# bootstrap indices against a running Elasticsearch node.
#
# Usage:
#   ./backend/elk/setup/bootstrap.sh [ES_HOST]
#
# Examples:
#   ./backend/elk/setup/bootstrap.sh                        # default localhost:9200
#   ES_HOST=http://elasticsearch:9200 ./backend/elk/setup/bootstrap.sh
#   ./backend/elk/setup/bootstrap.sh http://localhost:9200
#
# Safe to run multiple times. PUT operations are naturally idempotent;
# write-alias creation is skipped when the alias already exists.
# =============================================================================
set -euo pipefail

ES="${1:-${ES_HOST:-http://localhost:9200}}"
KIBANA="${KIBANA_URL:-http://localhost:5601}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ILM_DIR="${DIR}/../elasticsearch/ilm"
TPL_DIR="${DIR}/../elasticsearch/index-templates"
NDJSON="${DIR}/../kibana/saved-objects.ndjson"

# ── helpers ──────────────────────────────────────────────────────────────────
ok()   { printf "  \033[32m✓\033[0m %s\n" "$*"; }
info() { printf "  → %s\n" "$*"; }
skip() { printf "  \033[33m⊙\033[0m %s\n" "$*"; }
err()  { printf "  \033[31m✗\033[0m %s\n" "$*" >&2; }

curl_put() {
  local label="$1" url="$2" file="$3"
  info "${label}"
  local http_code
  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT "${url}" \
    -H "Content-Type: application/json" \
    --data-binary "@${file}")
  if [[ "${http_code}" =~ ^2 ]]; then
    ok "${label}  [HTTP ${http_code}]"
  else
    err "${label} failed  [HTTP ${http_code}]"
    return 1
  fi
}

alias_exists() {
  curl -sf -o /dev/null "${ES}/_alias/$1"
}

bootstrap_write_index() {
  local alias="$1"
  local index="${alias}-000001"
  if alias_exists "${alias}"; then
    skip "alias '${alias}' already exists — skipping"
    return
  fi
  info "creating bootstrap index ${index} with write alias '${alias}'"
  local http_code
  http_code=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT "${ES}/${index}" \
    -H "Content-Type: application/json" \
    -d "{\"aliases\":{\"${alias}\":{\"is_write_index\":true}}}")
  if [[ "${http_code}" =~ ^2 ]]; then
    ok "${index}  [HTTP ${http_code}]"
  else
    err "failed to create ${index}  [HTTP ${http_code}]"
    return 1
  fi
}

# ── wait for Elasticsearch ───────────────────────────────────────────────────
printf "\n=== DineOS ELK bootstrap ===\n"
printf "    Target : %s\n\n" "${ES}"

printf "Waiting for Elasticsearch...\n"
until curl -sf "${ES}/_cluster/health?wait_for_status=yellow&timeout=5s" \
  > /dev/null 2>&1; do
  sleep 3
done
ok "Elasticsearch is ready"
printf "\n"

# ── 1. ILM policy ────────────────────────────────────────────────────────────
printf "1/4  ILM policy\n"
curl_put \
  "dineos-logs-ilm-7d" \
  "${ES}/_ilm/policy/dineos-logs-ilm-7d" \
  "${ILM_DIR}/dineos-logs-ilm-7d.json"
printf "\n"

# ── 2. Index templates ───────────────────────────────────────────────────────
printf "2/4  Index templates\n"
curl_put \
  "dineos-api index template" \
  "${ES}/_index_template/dineos-api" \
  "${TPL_DIR}/dineos-api.json"

curl_put \
  "dineos-nginx index template" \
  "${ES}/_index_template/dineos-nginx" \
  "${TPL_DIR}/dineos-nginx.json"
printf "\n"

# ── 3. Write aliases ─────────────────────────────────────────────────────────
printf "3/4  Write aliases\n"
bootstrap_write_index "dineos-api-logs"
bootstrap_write_index "dineos-nginx-access"
printf "\n"

# ── 4. Import Kibana saved objects ─────────────────────────────────────────────
printf "4/4  Kibana saved objects\n"

printf "Waiting for Kibana...\n"
until curl -sf "${KIBANA}/api/status" > /dev/null 2>&1; do
  sleep 3
done
ok "Kibana is ready"

if [ ! -f "${NDJSON}" ]; then
  err "Saved objects file not found: ${NDJSON}"
  exit 1
fi

info "Importing saved objects (index patterns, searches, visualizations, dashboards)"
http_code=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${KIBANA}/api/saved_objects/_import?overwrite=true" \
  -H "kbn-xsrf: true" \
  --form file=@"${NDJSON}")
if [[ "${http_code}" =~ ^2 ]]; then
  ok "Kibana saved objects imported  [HTTP ${http_code}]"
else
  err "Kibana import failed  [HTTP ${http_code}]"
  exit 1
fi
printf "\n"

printf "=== Bootstrap complete ===\n\n"
printf "  Kibana         : %s\n" "${KIBANA}"
printf "  Elasticsearch  : %s\n\n" "${ES}"
