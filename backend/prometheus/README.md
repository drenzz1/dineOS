# Prometheus — DineOS Monitoring

Configuration and alert rules for the Prometheus + Alertmanager observability stack.

## Directory layout

```
backend/prometheus/
├── prometheus.yml          # Scrape config, rule file globs, Alertmanager target
├── alertmanager.yml        # Routing tree, Slack receiver, inhibition rules
└── rules/
    ├── api.rules.yml       # Alert group: dineos-api
    └── infra.rules.yml     # Alert group: dineos-infra
```

Prometheus mounts `prometheus.yml` and the entire `rules/` directory read-only.
Alertmanager mounts `alertmanager.yml` read-only.
Both paths are configured in `docker-compose.yml` at repo root.

---

## Alert groups

### `dineos-api` — `rules/api.rules.yml`

All api-group alerts carry `component=api, team=devops`.

| Alert | Severity | For | Threshold | Rationale |
|---|---|---|---|---|
| ApiDown | critical | 1 m | `up == 0` | The scrape itself fails — the API container is gone or the network path is broken. 1-minute `for` keeps noise low while still being fast to page. Inhibits all other `component=api` alerts (see alertmanager.yml). |
| ApiHighErrorRate | critical | 5 m | > 5 % 5xx | One in twenty requests returning a server error is customer-visible and warrants immediate attention. The 5-minute window smooths over transient spikes from rolling restarts. |
| ApiHighLatencyP95 | warning | 10 m | p95 > 1 s | The DineOS UX target is sub-second for interactive flows. Sustained p95 above 1 s indicates EF Core query regressions or downstream saturation. The 10-minute `for` avoids alerting on deployment warm-up. Correlate with `dineos_ef_command_duration_seconds` in Grafana. |
| DotnetGcHighPause | warning | 10 m | > 500 ms/s pause rate | Spending more than 50 % of CPU time in GC pauses causes latency spikes for all tenants simultaneously. Sustained 10 minutes rules out a single large allocation burst. |

**Metric sources**: `http_requests_received_total`, `http_request_duration_seconds_bucket`,
`up`, `dotnet_gc_pause_time_seconds_total` — all exposed by `prometheus-net.AspNetCore`
via `app.MapMetrics("/metrics")` in `DineOS.Api/Program.cs`.

---

### `dineos-infra` — `rules/infra.rules.yml`

Infrastructure alerts depend on **optional exporters** that are not yet added to
`docker-compose.yml`. The relevant scrape jobs in `prometheus.yml` are commented
out; enable the exporter container and uncomment the job together.

All infra-group alerts carry `team=devops`.

| Alert | Severity | For | Threshold | Component | Exporter needed |
|---|---|---|---|---|---|
| DatabaseUnavailable | critical | 2 m | `pg_up == 0` **or** metric absent | database | `prometheuscommunity/postgres-exporter` |
| RabbitMqQueueBacklog | warning | 10 m | > 1,000 ready messages | rabbitmq | Built-in `rabbitmq_prometheus` plugin (port 15692) |
| DiskSpaceLow | warning | 15 m | root fs < 10 % free | node | `prom/node-exporter` |
| HighMemoryPressure | warning | 15 m | memory use > 90 % | node | `prom/node-exporter` |

**DatabaseUnavailable** uses `pg_up == 0 OR absent(pg_up)` to catch two failure
modes: exporter running but DB unreachable, and exporter itself not running.
The `absent()` branch fires with value `1` and no instance label, so the alert
description avoids `$labels.instance`.

**RabbitMqQueueBacklog** targets the `dineos.orders.created.notifications` queue
primarily. A sustained backlog there delays kitchen-display notifications for new
orders.  The RabbitMQ management plugin exposes per-queue metrics at
`rabbitmq:15692/metrics` without a separate exporter binary.

---

## Inhibition rules

Defined in `alertmanager.yml`:

```
ApiDown (critical) → suppresses all component=api, severity=warning|critical alerts
                      on the same cluster + env
```

When the API container is entirely unreachable, latency and error-rate alerts are
meaningless (every request is failing). Routing two pages for the same root cause
causes alert fatigue. The inhibition rule ensures only `ApiDown` pages on-call.

---

## Adding a new alert

1. Create or append to a `rules/*.rules.yml` file.
2. Every alert **must** include:
   - `labels.severity` — `critical` or `warning`
   - `labels.component` — the subsystem (`api`, `database`, `rabbitmq`, `node`, …)
   - `labels.team: devops`
   - `annotations.summary` — one-line human description
   - `annotations.description` — detail with `{{ $labels.* }}` and `{{ $value | humanize* }}`
   - `annotations.runbook_url` — link to the runbook wiki page
3. Reload Prometheus without restart: `curl -X POST http://localhost:9090/-/reload`
   (requires `--web.enable-lifecycle` which is set in `docker-compose.yml`).

---

## Enabling placeholder scrape jobs

| Job | Step |
|---|---|
| `node-exporter` | Add the service snippet from the comment in `prometheus.yml`, then uncomment the scrape job. |
| `postgres` | Add `prometheuscommunity/postgres-exporter` with `DATA_SOURCE_NAME` env var, then uncomment. |
| `rabbitmq` | Confirm `rabbitmq_prometheus` plugin is active (`rabbitmq-plugins list`), then uncomment. |

---

## Backend webhook and AI triage (DO-12)

Alertmanager's default receiver (`dineos-webhook`) routes all firing alerts to
the dineOS API at `POST /api/v1/alerts/webhook`.  The backend runs AI triage
and posts a structured Slack message with the result.

Key configuration:

| Variable | Description |
|---|---|
| `ALERT_WEBHOOK_SECRET` | Shared secret — set in `.env` and matched by `AlertWebhook:SharedSecret` in the API. Leave empty on a closed Docker network. |
| `Anthropic__ApiKey` (or OpenAI / Google) | AI provider key for triage. Leave blank to disable AI; the webhook still returns 200. |
| `SLACK_WEBHOOK_URL` | Used by both the API's `SlackNotifier` and the `slack-direct` fallback receiver. |

To fall back to direct Alertmanager → Slack (without AI triage), change
`route.receiver` in `alertmanager.yml` to `slack-direct`.

See [docs/devops/aiops-triage.md](../../docs/devops/aiops-triage.md) for the
full architecture diagram, secret setup, demo steps, and failure-path behavior.

---

## Slack webhook setup

Set `SLACK_WEBHOOK_URL` in `.env` (never commit the real value).
The alertmanager container's entrypoint (in `docker-compose.yml`) substitutes
`${SLACK_WEBHOOK_URL}` and `${ALERT_WEBHOOK_SECRET}` into a runtime copy of the
config with `sed` at startup (Alertmanager has no built-in env-var expansion).
