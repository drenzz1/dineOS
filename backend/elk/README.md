# ELK — DineOS Log Aggregation

Configuration for the Elasticsearch + Logstash + Kibana log-aggregation stack.
Runs as an **opt-in Docker Compose profile** (`--profile elk`) and does not
start with a default `docker compose up -d`.

## Directory layout

```
backend/elk/
├── elasticsearch/
│   ├── ilm/
│   │   └── dineos-logs-ilm-7d.json       # ILM policy: hot 1 d → delete 7 d
│   └── index-templates/
│       ├── dineos-api.json               # Mapping template for API log indices
│       └── dineos-nginx.json             # Mapping template for Nginx access indices
├── kibana/
│   └── saved-objects.ndjson              # Pre-built dashboards, searches, index patterns
├── logstash/
│   ├── config/
│   │   └── logstash.yml                  # Logstash node config (http.host, xpack)
│   └── pipeline/
│       └── dineos.conf                   # Ingest pipeline (inputs, filters, outputs)
├── setup/
│   └── bootstrap.sh                      # Idempotent ES + Kibana provisioning script
└── README.md
```

Logstash mounts `logstash/config/logstash.yml` and the entire `logstash/pipeline/`
directory read-only.  The `elasticsearch/` subtree is used only by `bootstrap.sh`
at setup time — it is not mounted into any container.

---

## File reference

### `elasticsearch/ilm/dineos-logs-ilm-7d.json`

ILM policy suitable for local and demo environments.

| Phase | Trigger | Action |
|---|---|---|
| hot | index creation | roll over after **1 day** or **5 GB** (whichever comes first) |
| delete | 7 days after rollover | permanently delete the index |

Total retention is approximately 8 days. For production, increase `max_age` in
the hot phase and `min_age` in the delete phase, or add a `warm` phase with
`forcemerge` and `shrink` actions.

### `elasticsearch/index-templates/dineos-api.json`

Composite index template matching `dineos-api-logs-*`.

| Field | Type | Notes |
|---|---|---|
| `@timestamp` | `date` | Set from Serilog `Timestamp` by Logstash `date` filter |
| `service` | `keyword` | Always `dineos-api` |
| `level` | `keyword` | Lowercased by Logstash (`information`, `warning`, `error`) |
| `CorrelationId` | `keyword` | Trace correlation across request boundaries |
| `UserId` | `keyword` | Authenticated user identity |
| `TenantId` | `keyword` | Multi-tenant discriminator |
| `StatusCode` | `keyword` | HTTP response code from Serilog enricher |
| `RequestPath` | `keyword` | Request path without query string |
| `RequestMethod` | `keyword` | HTTP verb |
| `SourceContext` | `keyword` | Serilog source context (class name) |
| `Elapsed` | `long` | Request duration in milliseconds |

### `elasticsearch/index-templates/dineos-nginx.json`

Composite index template matching `dineos-nginx-access-*`.

| Field | Type | Notes |
|---|---|---|
| `@timestamp` | `date` | From Filebeat event time |
| `service` | `keyword` | Always `dineos-nginx` |
| `remote_addr` | `ip` | Client IP — enables geo queries and IP range filters |
| `request_method` | `keyword` | HTTP verb |
| `request_uri` | `keyword` | Full URI path + query string |
| `status` | `keyword` | HTTP status code |
| `body_bytes_sent` | `long` | Response size in bytes |
| `request_time` | `float` | Request duration in **seconds** (converted from ms by Logstash) |
| `request_time_ms` | `long` | Request duration in **milliseconds** (raw value for histogram buckets) |
| `http_user_agent` | `keyword` | Raw user-agent string; parsed by Logstash into `ua.*` |
| `http_referer` | `keyword` | HTTP Referer header |

### `kibana/saved-objects.ndjson`

NDJSON export suitable for `POST /api/saved_objects/_import?overwrite=true`.
Contains everything needed for out-of-the-box dashboards:

**Index patterns** (2)
| Title | Matching |
|---|---|
| `dineos-api-logs-*` | API log indices |
| `dineos-nginx-access-*` | Nginx access-log indices |

**Saved searches** (4)
| Title | Purpose |
|---|---|
| API Errors (Last 1h) | `level:error` in the last hour |
| API by CorrelationId | All API events grouped by CorrelationId |
| Nginx 5xx (Last 1h) | `status >= 500` in the last hour |
| Nginx Latency p95 by Path | Recent requests sorted by response time |

**Dashboards** (2)
| Title | Panels |
|---|---|
| DineOS API Logs | Status code histogram, top request paths, error rate over time, top exceptions/SourceContext |
| DineOS Nginx Access | Status code histogram, top request URIs (avg + p95), geo map (client IP), p95 latency by path |

### `logstash/config/logstash.yml`

Minimal Logstash node config: binds the monitoring API to `0.0.0.0:9600` and
disables X-Pack monitoring (not available without a licence).

### `logstash/pipeline/dineos.conf`

Single pipeline that handles both log sources.

**Inputs**

| Port | Protocol | Source | Tag |
|---|---|---|---|
| 5001 | TCP / `json_lines` codec | Serilog structured logs from the .NET API | `[@metadata][pipeline] = serilog` |
| 5044 | Beats | Filebeat shipping Nginx JSON access logs | `[@metadata][pipeline] = nginx` |

**Filter — Serilog branch**

1. `date` filter parses `Timestamp` (ISO 8601) → `@timestamp`.
2. `mutate rename` promotes `Level` → `level` and all `Properties.*` fields
   to top-level (`CorrelationId`, `RequestPath`, `UserId`, `TenantId`,
   `SourceContext`, `RequestMethod`, `StatusCode`, `Elapsed`).
3. Second `mutate lowercase` normalises `level` — required because Logstash
   applies `rename` before `lowercase` within a single `mutate` block.
4. Adds `service = "dineos-api"`.

**Filter — Nginx branch**

1. `grok` extracts `remote_addr`, `request_method`, `request_uri`, `status`,
   `body_bytes_sent`, `request_time`, `http_referer`, `http_user_agent` from
   the JSON access-log line produced by Nginx's `json_combined` log format.
2. `mutate convert` casts `status` → integer, `body_bytes_sent` → integer,
   `request_time` → float.
3. `request_time_ms` is copied from the raw ms value (long) before a `ruby`
   block divides `request_time` by 1000 to produce the seconds float.
4. `geoip` enriches `remote_addr` → `geo.*`.
5. `useragent` parses `http_user_agent` → `ua.*`.
6. Adds `service = "dineos-nginx"`.

**Output**

Routes on `[service]` to two write aliases backed by ILM-managed indices:

| Service | Alias / index pattern | ILM policy |
|---|---|---|
| `dineos-api` | `dineos-api-logs` → `dineos-api-logs-YYYY.MM.dd` | `dineos-logs-ilm-7d` |
| `dineos-nginx` | `dineos-nginx-access` → `dineos-nginx-access-YYYY.MM.dd` | `dineos-logs-ilm-7d` |

### `setup/bootstrap.sh`

Idempotent shell script that registers all Elasticsearch assets and imports
Kibana saved objects in order:

1. **ILM policy** — `PUT /_ilm/policy/dineos-logs-ilm-7d`
2. **Index templates** — `PUT /_index_template/dineos-api` and `dineos-nginx`
3. **Write aliases** — creates `dineos-api-logs-000001` and
   `dineos-nginx-access-000001` with `is_write_index: true` if the alias does
   not already exist.
4. **Kibana saved objects** — waits for Kibana, then POSTs
   `kibana/saved-objects.ndjson` to `/api/saved_objects/_import?overwrite=true`
   with the required `kbn-xsrf: true` header.

Safe to run repeatedly. PUT operations are idempotent; the alias step is
guarded by an existence check; Kibana import uses `overwrite=true`.

---

## Quick start

```bash
# 1. Start the ELK profile
docker compose --profile elk up -d

# 2. Wait for Elasticsearch to be healthy, then bootstrap
./backend/elk/setup/bootstrap.sh

# 3. Open Kibana
open http://localhost:5601
```

To override the Elasticsearch host (e.g. when running bootstrap from inside
another container):

```bash
ES_HOST=http://elasticsearch:9200 ./backend/elk/setup/bootstrap.sh
```

To override the Kibana URL:

```bash
KIBANA_URL=http://kibana:5601 ./backend/elk/setup/bootstrap.sh
```

---

## Ports

| Service | Host port | Variable | UI |
|---|---|---|---|
| Elasticsearch | 9200 | `ES_PORT` | `http://localhost:9200` |
| Logstash Beats | 5044 | — | — |
| Logstash TCP/JSON | 5001 | — | — |
| Logstash API | 9600 | — | `http://localhost:9600` |
| Kibana | 5601 | `KIBANA_PORT` | `http://localhost:5601` |

> **Port conflict**: Logstash's TCP JSON input defaults to host port 5001, the
> same as `API_HTTP_PORT`. When running both stacks simultaneously, set
> `API_HTTP_PORT=5002` (or another free port) in your `.env`.

---

## Security note

`xpack.security.enabled=false` and no authentication are intentional for
local and demo use. **Do not expose this stack on a public network or use
these settings in production.**  For production, enable X-Pack security,
configure TLS, and set up Elasticsearch passwords before starting the cluster.
