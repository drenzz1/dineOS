#!/usr/bin/env bash
# =============================================================================
# DineOS Uptime Kuma bootstrap — idempotent
#
# 1. Waits for Kuma to be healthy
# 2. Creates the admin account on first run (no-op if already configured)
# 3. Authenticates and obtains a Bearer token
# 4. Creates notification channels: Slack + Email/SMTP (Mailhog)
#    — skipped if any notifications already exist (re-run safety)
# 5. Creates 7 monitors via the Kuma REST API (v1.23+)
#    — skipped if any monitors already exist (re-run safety)
# 6. Prints the one manual step: import kuma-backup.json to create the
#    "dineOS Services" status page (no REST endpoint for status pages in 1.x)
#
# Usage:
#   ./backend/uptime-kuma/setup/bootstrap.sh
#   KUMA_URL=http://localhost:3001 SLACK_WEBHOOK_URL=https://hooks.slack.com/... \
#     ./backend/uptime-kuma/setup/bootstrap.sh
#
# Environment variables:
#   KUMA_URL                Uptime Kuma base URL       (default: http://localhost:3001)
#   KUMA_ADMIN_USER         Admin username              (default: admin)
#   KUMA_ADMIN_PASSWORD     Admin password              (default: admin)
#   SLACK_WEBHOOK_URL       Slack Incoming Webhook URL  (default: placeholder)
#   UPTIME_KUMA_SMTP_TO     Alert recipient address     (default: ops@dineos.local)
#
# SLACK_WEBHOOK_URL is shared with Alertmanager — set it once in .env and both
# services pick it up.  The placeholder value disables real Slack delivery but
# lets you test the Kuma UI locally without any secrets.
#
# For full reproducibility including the status page, use the backup import
# path documented in backend/uptime-kuma/README.md instead.
# =============================================================================
set -euo pipefail

KUMA="${1:-${KUMA_URL:-http://localhost:3001}}"
KUMA_USER="${KUMA_ADMIN_USER:-admin}"
KUMA_PASS="${KUMA_ADMIN_PASSWORD:-admin}"
SLACK_WEBHOOK="${SLACK_WEBHOOK_URL:-https://hooks.slack.com/services/TXXXXXXXX/BXXXXXXXX/placeholder}"
SMTP_TO="${UPTIME_KUMA_SMTP_TO:-ops@dineos.local}"
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKUP_JSON="${DIR}/../kuma-backup.json"

# ── helpers ──────────────────────────────────────────────────────────────────
ok()   { printf "  \033[32m✓\033[0m %s\n" "$*"; }
info() { printf "  → %s\n" "$*"; }
skip() { printf "  \033[33m⊙\033[0m %s\n" "$*"; }
err()  { printf "  \033[31m✗\033[0m %s\n" "$*" >&2; }

post_json() {
  # post_json <url> [bearer_token] <json_body>
  # Prints the HTTP response body; exits non-zero on curl error only.
  local url="$1" token="$2" body="$3"
  curl -s \
    -X POST "${url}" \
    -H "Content-Type: application/json" \
    ${token:+-H "Authorization: Bearer ${token}"} \
    -d "${body}"
}

http_code() {
  # http_code <url> [bearer_token] <json_body>
  # Prints just the HTTP status code.
  local url="$1" token="$2" body="$3"
  curl -s -o /dev/null -w "%{http_code}" \
    -X POST "${url}" \
    -H "Content-Type: application/json" \
    ${token:+-H "Authorization: Bearer ${token}"} \
    -d "${body}"
}

# ── 0. Wait for Kuma ─────────────────────────────────────────────────────────
printf "\n=== DineOS Uptime Kuma bootstrap ===\n"
printf "    Target : %s\n\n" "${KUMA}"

printf "Waiting for Uptime Kuma...\n"
until curl -sf "${KUMA}/api/entry-page" > /dev/null 2>&1; do
  sleep 3
done
ok "Uptime Kuma is ready"
printf "\n"

# ── 1. Admin account setup ───────────────────────────────────────────────────
printf "1/6  Admin account\n"
info "Creating admin account (no-op if already configured)"
setup_code=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${KUMA}/setup" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=${KUMA_USER}&password=${KUMA_PASS}")

if [[ "${setup_code}" == "200" ]]; then
  ok "Admin account created  [HTTP 200]"
elif [[ "${setup_code}" == "400" ]]; then
  skip "Admin already configured  [HTTP 400]"
else
  err "Unexpected response from /setup  [HTTP ${setup_code}]"
  exit 1
fi
printf "\n"

# ── 2. Authenticate ──────────────────────────────────────────────────────────
printf "2/6  Authentication\n"
info "Obtaining Bearer token"
token_response=$(curl -sf \
  -X POST "${KUMA}/login/access-token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=${KUMA_USER}&password=${KUMA_PASS}&token=")

TOKEN=$(echo "${token_response}" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
if [[ -z "${TOKEN}" ]]; then
  err "Failed to obtain token — check KUMA_ADMIN_USER / KUMA_ADMIN_PASSWORD"
  exit 1
fi
ok "Authenticated as '${KUMA_USER}'"
printf "\n"

# ── 3. Notification channels ─────────────────────────────────────────────────
printf "3/6  Notification channels\n"

existing_notif=$(curl -sf \
  -H "Authorization: Bearer ${TOKEN}" \
  "${KUMA}/api/v1/notifications" 2>/dev/null || echo '{"notifications":[]}')
notif_count=$(echo "${existing_notif}" | grep -o '"id"' | wc -l | tr -d ' ')

if [[ "${notif_count}" -gt 0 ]]; then
  skip "Notification channels already present (found ${notif_count}) — skipping"
  printf "\n"
else
  info "Creating Slack notification channel"
  # webhookURL is read from SLACK_WEBHOOK_URL; the config value is kept in
  # the backup JSON as a placeholder — never commit a real webhook URL.
  slack_body=$(printf '{
    "type": "slack",
    "name": "Slack – #dineos-alerts",
    "isDefault": true,
    "active": true,
    "webhookURL": "%s",
    "channel": "#dineos-alerts",
    "username": "Uptime Kuma",
    "iconEmoji": ":rotating_light:"
  }' "${SLACK_WEBHOOK}")
  slack_code=$(http_code "${KUMA}/api/v1/notifications" "${TOKEN}" "${slack_body}")
  if [[ "${slack_code}" =~ ^2 ]]; then
    ok "Slack channel created  [HTTP ${slack_code}]"
  elif [[ "${slack_code}" == "404" ]]; then
    skip "REST endpoint not available — add notification via UI (Settings → Notifications)"
  else
    err "Slack channel creation failed  [HTTP ${slack_code}]"
  fi

  info "Creating SMTP/Email notification channel (Mailhog)"
  # Mailhog runs at mailhog:1025 with no auth — safe for local/demo use only.
  # For production: set hostname, port, security, username, password in the Kuma UI.
  smtp_body=$(printf '{
    "type": "smtp",
    "name": "Email – Mailhog (SMTP)",
    "isDefault": false,
    "active": true,
    "hostname": "mailhog",
    "port": "1025",
    "security": "None",
    "ignoreTLSError": false,
    "username": "",
    "password": "",
    "fromAddress": "uptime-kuma@dineos.local",
    "toAddress": "%s"
  }' "${SMTP_TO}")
  smtp_code=$(http_code "${KUMA}/api/v1/notifications" "${TOKEN}" "${smtp_body}")
  if [[ "${smtp_code}" =~ ^2 ]]; then
    ok "SMTP channel created  [HTTP ${smtp_code}]"
  elif [[ "${smtp_code}" == "404" ]]; then
    skip "REST endpoint not available — add notification via UI (Settings → Notifications)"
  else
    err "SMTP channel creation failed  [HTTP ${smtp_code}]"
  fi
  printf "\n"
fi

# ── 4. Monitors ──────────────────────────────────────────────────────────────
printf "4/6  Monitors\n"

existing=$(curl -sf \
  -H "Authorization: Bearer ${TOKEN}" \
  "${KUMA}/api/v1/monitors" 2>/dev/null || echo '{"monitors":[]}')
mon_count=$(echo "${existing}" | grep -o '"id"' | wc -l | tr -d ' ')

if [[ "${mon_count}" -gt 0 ]]; then
  skip "Monitors already present (found ${mon_count}) — skipping creation"
  printf "\n"
else
  info "No existing monitors found — creating 7 monitors"
  printf "\n"

  create_monitor() {
    local name="$1" body="$2"
    info "${name}"
    local code
    code=$(http_code "${KUMA}/api/v1/monitors" "${TOKEN}" "${body}")
    if [[ "${code}" =~ ^2 ]]; then
      ok "${name}  [HTTP ${code}]"
    else
      err "${name} failed  [HTTP ${code}]"
      return 1
    fi
  }

  create_monitor "Frontend" \
    '{"type":"http","name":"Frontend","description":"Next.js UI — Docker Compose internal hostname frontend:3000","url":"http://frontend:3000","method":"GET","interval":60,"retryInterval":20,"maxretries":3,"accepted_statuscodes":["200-299"],"timeout":48,"maxredirects":10}'

  create_monitor "API – Liveness" \
    '{"type":"http","name":"API – Liveness","description":"ASP.NET Core API process is alive — returns 200 when the runtime is up","url":"http://api:8080/api/v1/health","method":"GET","interval":30,"retryInterval":10,"maxretries":3,"accepted_statuscodes":["200-299"],"timeout":48,"maxredirects":10}'

  create_monitor "API – Readiness (DB)" \
    '{"type":"keyword","name":"API – Readiness (DB)","description":"All .NET health checks pass including PostgreSQL. Keyword Healthy must appear in the response body.","url":"http://api:8080/api/v1/health","method":"GET","keyword":"Healthy","interval":30,"retryInterval":10,"maxretries":3,"accepted_statuscodes":["200-299"],"timeout":48,"maxredirects":10}'

  create_monitor "Keycloak" \
    '{"type":"http","name":"Keycloak","description":"Keycloak 24 identity provider — Quarkus /health/ready readiness probe","url":"http://keycloak:8080/health/ready","method":"GET","interval":60,"retryInterval":20,"maxretries":3,"accepted_statuscodes":["200-299"],"timeout":48,"maxredirects":10}'

  create_monitor "Grafana" \
    '{"type":"http","name":"Grafana","description":"Grafana observability dashboard — /api/health returns database status","url":"http://grafana:3000/api/health","method":"GET","interval":60,"retryInterval":20,"maxretries":3,"accepted_statuscodes":["200-299"],"timeout":48,"maxredirects":10}'

  create_monitor "RabbitMQ – Management" \
    '{"type":"http","name":"RabbitMQ – Management","description":"RabbitMQ management UI is reachable on port 15672","url":"http://rabbitmq:15672","method":"GET","interval":60,"retryInterval":20,"maxretries":3,"accepted_statuscodes":["200-299"],"timeout":48,"maxredirects":10}'

  create_monitor "RabbitMQ – AMQP" \
    '{"type":"tcp","name":"RabbitMQ – AMQP","description":"AMQP broker TCP port 5672 — verifies the messaging layer accepts connections","hostname":"rabbitmq","port":5672,"interval":30,"retryInterval":10,"maxretries":3,"timeout":48}'

  printf "\n"
fi

# ── 5. Notification–monitor linking note ─────────────────────────────────────
printf "5/6  Notification–monitor linking\n"
info "Monitors created by the REST API do not have notifications auto-linked."
info "To link all monitors to both channels in one shot, use the backup import:"
info "  Settings → Backup → Import → select kuma-backup.json"
printf "\n"

# ── 6. Status page (manual step) ─────────────────────────────────────────────
printf "6/6  Status page — manual import required\n"
info "Uptime Kuma 1.x has no REST endpoint for status page creation."
info "Import the pre-built backup to create the 'dineOS Services' status page:"
printf "\n"
printf "    1. Open  %s\n" "${KUMA}"
printf "    2. Complete the first-login wizard (if not already done)\n"
printf "    3. Settings → Backup → Import\n"
printf "    4. Select: %s\n" "${BACKUP_JSON}"
printf "    5. Click Import — all monitors, notifications, and status page are restored\n"
printf "\n"
printf "    Status page will be available at:\n"
printf "    %s/status/dineos\n" "${KUMA}"
printf "\n"
ok "Bootstrap complete"
printf "\n"
printf "=== Summary ===\n\n"
printf "  Uptime Kuma  : %s\n" "${KUMA}"
printf "  Admin user   : %s\n" "${KUMA_USER}"
printf "  Slack target : #dineos-alerts\n"
if [[ "${SLACK_WEBHOOK}" == *"placeholder"* ]]; then
  printf "  Slack webhook: PLACEHOLDER — set SLACK_WEBHOOK_URL for real alerts\n"
else
  printf "  Slack webhook: configured\n"
fi
printf "  SMTP target  : %s (via mailhog:1025)\n" "${SMTP_TO}"
printf "  Backup JSON  : %s\n\n" "${BACKUP_JSON}"
