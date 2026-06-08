# Uptime Kuma — DineOS Service Monitoring

Configuration and seed data for the Uptime Kuma instance that monitors all
dineOS platform services.  Runs as an **opt-in Docker Compose profile**
(`--profile uptime`) and does not start with a default `docker compose up -d`.

## Directory layout

```
backend/uptime-kuma/
├── kuma-backup.json          # Kuma 1.23.x backup — canonical monitors, notifications, status page
├── setup/
│   └── bootstrap.sh          # Idempotent: admin setup, notification channels, monitors
└── README.md
```

---

## Monitor reference

Seven monitors run against Docker Compose internal hostnames and therefore
require the `uptime-kuma` container to be on the `dineos-net` network
(handled automatically by `docker-compose.yml`).

| # | Name | Type | Target | Interval | Purpose |
|---|------|------|--------|----------|---------|
| 1 | Frontend | HTTP | `http://frontend:3000` | 60 s | Next.js UI reachable, 200–299 |
| 2 | API – Liveness | HTTP | `http://api:8080/api/v1/health` | 30 s | ASP.NET process alive, 200–299 |
| 3 | API – Readiness (DB) | Keyword | `http://api:8080/api/v1/health` | 30 s | All .NET health checks pass — keyword `Healthy` must appear in the JSON body |
| 4 | Keycloak | HTTP | `http://keycloak:8080/health/ready` | 60 s | Quarkus readiness probe, 200–299 |
| 5 | Grafana | HTTP | `http://grafana:3000/api/health` | 60 s | Grafana API health, 200–299 |
| 6 | RabbitMQ – Management | HTTP | `http://rabbitmq:15672` | 60 s | Management UI responding, 200–299 |
| 7 | RabbitMQ – AMQP | TCP | `rabbitmq:5672` | 30 s | AMQP broker accepts TCP connections |

All monitors are linked to both notification channels (Slack + Email) in the
backup JSON, so an alert fires on **both** channels on state change.

---

## Notification channels

### Channel 1 — Slack (`#dineos-alerts`)

| Field | Dev / demo value | Production |
|-------|-----------------|------------|
| Type | Slack Incoming Webhook | Slack Incoming Webhook |
| Webhook URL | `https://hooks.slack.com/services/TXXXXXXXX/BXXXXXXXX/placeholder` | Real webhook from your Slack App |
| Channel | `#dineos-alerts` | Change as needed |
| Icon | `:rotating_light:` | Change as needed |
| Is default | Yes | Yes |

The webhook URL is read from the `SLACK_WEBHOOK_URL` environment variable by
`bootstrap.sh`, and is the **same variable already used by Alertmanager** — set
it once in `.env` and both services pick it up.  The placeholder value in the
backup JSON allows the Kuma UI to load and test locally without real credentials;
Slack will silently reject the placeholder webhook, which is the correct
behaviour for local dev.

**To use a real webhook:**
1. Create a Slack App at `api.slack.com/apps` → Incoming Webhooks → Activate.
2. Copy the generated webhook URL.
3. Set it in `.env`: `SLACK_WEBHOOK_URL=https://hooks.slack.com/services/T.../B.../...`
4. Re-run `bootstrap.sh`, or edit the channel in-place via
   **Settings → Notifications → Slack – #dineos-alerts → Edit**.
5. Click **Test** to verify delivery.

### Channel 2 — Email/SMTP (`Mailhog`)

| Field | Dev / demo value | Production |
|-------|-----------------|------------|
| Type | SMTP | SMTP |
| Hostname | `mailhog` | Your SMTP relay (e.g. `smtp.sendgrid.net`) |
| Port | `1025` | `587` (STARTTLS) or `465` (TLS) |
| Security | None | `STARTTLS` or `TLS` |
| Username | _(empty)_ | SMTP username / API key |
| Password | _(empty)_ | SMTP password / API key — **never commit** |
| From address | `uptime-kuma@dineos.local` | A verified sender address |
| To address | `ops@dineos.local` | Real recipient; override with `UPTIME_KUMA_SMTP_TO` |

Mailhog catches all outbound email locally — view sent alerts at
`http://localhost:8025`.  No configuration changes are needed for local dev;
email flows automatically once the stack is running.

**To use a real SMTP relay in production:**
1. Edit the notification in the Kuma UI:
   **Settings → Notifications → Email – Mailhog (SMTP) → Edit**.
2. Update hostname, port, security, username, and password.
3. Click **Test** to verify delivery.
4. Never commit real SMTP credentials — configure them in the live Kuma
   instance or inject via environment at runtime.

---

## Quick start

```bash
# 1. Start the uptime profile
docker compose --profile uptime up -d

# 2. Seed everything via bootstrap
SLACK_WEBHOOK_URL=https://hooks.slack.com/services/... \
  ./backend/uptime-kuma/setup/bootstrap.sh

# 3. Complete the status page (one manual step — see below)

# 4. Open Uptime Kuma
open http://localhost:3001
```

For local dev without a real Slack webhook, simply omit `SLACK_WEBHOOK_URL`
and the bootstrap uses the placeholder — monitors still work, Slack alerts
are silently dropped.

---

## Importing `kuma-backup.json` (recommended — full one-shot setup)

The backup JSON restores **all monitors, both notification channels, and the
status page** in a single operation, with all monitor→notification links
already wired.  Use this path on a fresh instance.

1. Start the container: `docker compose --profile uptime up -d`
2. Open `http://localhost:3001` and complete the first-login wizard.
3. Navigate to **Settings → Backup**.
4. Under **Import**, click **Choose file** and select
   `backend/uptime-kuma/kuma-backup.json`.
5. Click **Import**.

After import:
- All 7 monitors are active and linked to both notification channels.
- The **dineOS Services** status page is at
  `http://localhost:3001/status/dineos`.
- The Slack channel uses the **placeholder webhook** — update it for real
  alerts (see "To use a real webhook" above).

> **Note:** The backup import merges into any existing data using the IDs
> stored in the JSON.  Run on a fresh instance to avoid ID conflicts.

---

## Exporting an updated backup

After making changes in the Kuma UI (adding monitors, editing intervals,
updating notification credentials, etc.) export a fresh backup to keep the
repo in sync:

1. **Settings → Backup → Export**
2. Save the downloaded JSON to `backend/uptime-kuma/kuma-backup.json`,
   replacing the existing file.
3. **Before committing:** confirm the backup contains no real secrets.
   The Slack webhook URL and any SMTP credentials must remain as
   placeholders in the committed file.

```bash
# Quick check — should print only the placeholder, not a real URL
grep -o '"webhookURL":"[^"]*"' backend/uptime-kuma/kuma-backup.json
```

---

## `setup/bootstrap.sh`

Idempotent shell script that provisions Kuma via the REST API (v1.23+).

| Step | Action |
|------|--------|
| 1 | Waits for `GET /api/entry-page` to return 200 |
| 2 | `POST /setup` — creates admin account; 400 = already configured (no-op) |
| 3 | `POST /login/access-token` — obtains Bearer token |
| 4 | `POST /api/v1/notifications` × 2 — Slack + SMTP; skips if any notifications exist |
| 5 | `POST /api/v1/monitors` × 7 with `notificationIDList` wired to both channels — skips if any monitors exist |
| 6 | Prints backup import instructions for status page creation |

**Limitation:** Uptime Kuma 1.x has no REST endpoint for creating status pages.
Monitor–notification links are handled directly in the `POST /api/v1/monitors`
payload via `notificationIDList`, so the backup import is only needed to restore
the pre-built **dineOS Services** status page.

If the REST notifications endpoint returns 404 (older patch version), the
script prints instructions to add channels via **Settings → Notifications**
in the UI.

---

## `kuma-backup.json`

Uptime Kuma 1.23.x backup export format.

| Key | Contents |
|-----|----------|
| `version` | Kuma version string |
| `monitors` | 7 monitor objects — each with `notificationIDList: {1: true, 2: true}` |
| `notificationList` | 2 channels: Slack (id 1, default) + SMTP Mailhog (id 2) |
| `statusPageList` | One status page (`slug: dineos`) with all 7 monitors linked |
| `proxyList` | Empty |
| `apiKeyList` | Empty |
| `maintenanceList` | Empty |
| `dockerHostList` | Empty |

The `config` field of each notification is a **JSON string** (Kuma's internal
SQLite format).  The Slack `webhookURL` is the placeholder value — swap it in
the UI or via `bootstrap.sh` using `SLACK_WEBHOOK_URL`.

---

## Ports

| Service | Host port | Variable | UI |
|---------|-----------|----------|----|
| Uptime Kuma | 3001 | `UPTIME_KUMA_PORT` | `http://localhost:3001` |
| Status page | 3001 | — | `http://localhost:3001/status/dineos` |
| Alert emails (Mailhog) | 8025 | `MAILHOG_UI_PORT` | `http://localhost:8025` |

---

## Security notes

- **Slack webhook URL** — treat as a secret.  Set `SLACK_WEBHOOK_URL` in `.env`
  (git-ignored); never commit a real URL to version control.  The placeholder
  stored in `kuma-backup.json` is intentionally non-functional.
- **SMTP credentials** — configure in the live Kuma UI only; do not store them
  in `kuma-backup.json`.  Mailhog (the local dev SMTP sink) has no auth, so no
  credentials are needed for local use.
- **Admin password** — change via **Settings → Security → Change Password**
  before exposing the port on a shared or public network.  The default
  `admin` / `admin` pair is intentional for local development only.
- **Persistent data** — all Kuma state (monitors, history, notification config)
  is stored in the `uptime_kuma_data` named Docker volume at `/app/data/kuma.db`.
  Back up this volume before wiping it if you have production history to preserve.
