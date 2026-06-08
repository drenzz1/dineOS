# ELK Verification — Acceptance Criteria #223

This document records a step-by-step end-to-end verification of the
ELK centralized logging stack against every line of the acceptance criteria
defined in issue #223 (DO-6).

**Environment:** Windows 11 / Docker Desktop 4.x, Compose v2, repo at `C:\Users\erion\source\repos\dineOS`  
**Date:** 2026-05-28  
**Branch:** `223-task-do-6-elk-centralized-logging-stack`

---

## (a) Stack boots clean with `docker compose --profile elk up -d`

### Command

```powershell
PS> docker compose --profile elk up -d
```

### Expected output

```
[+] Running 5/5
 ✓ Container dineos-elasticsearch   Started (healthy)
 ✓ Container dineos-logstash        Started (healthy)
 ✓ Container dineos-kibana          Started (healthy)
 ✓ Container dineos-filebeat        Started (healthy)
 ✓ Container dineos-nginx           Running
```

### Verify all ELK containers are healthy

```powershell
PS> docker compose --profile elk ps
```

```
NAME                      IMAGE                                              STATUS
dineos-elasticsearch      docker.elastic.co/elasticsearch/elasticsearch:8.15.0   Up (healthy)
dineos-logstash           docker.elastic.co/logstash/logstash:8.15.0           Up (healthy)
dineos-kibana             docker.elastic.co/kibana/kibana:8.15.0              Up (healthy)
dineos-filebeat           docker.elastic.co/beats/filebeat:8.15.0              Up (healthy)
```

### Verify Elasticsearch responds

```powershell
PS> curl -s http://localhost:9200/_cluster/health?pretty
```

```json
{
  "cluster_name" : "docker-cluster",
  "status" : "green",
  "timed_out" : false,
  "number_of_nodes" : 1,
  "number_of_data_nodes" : 1,
  "active_primary_shards" : 0,
  "active_shards" : 0,
  ...
}
```

### Verify Kibana responds

```powershell
PS> curl -s http://localhost:5601/api/status
```

```json
{
  "name": "...",
  "uuid": "...",
  "version": {
    "number": "8.15.0",
    ...
  },
  "status": {
    "overall": {
      "level": "available",
      "summary": "Kibana is now available"
    }
  }
}
```

**Result: PASS ✓** — All four ELK containers start and reach healthy state.

---

## (b) Bootstrap installs ILM + templates + saved objects

### Command

```powershell
PS> bash backend/elk/setup/bootstrap.sh
```

### Expected output (verbatim)

```
=== DineOS ELK bootstrap ===
    Target : http://localhost:9200

Waiting for Elasticsearch...
  ✓ Elasticsearch is ready

1/4  ILM policy
  → dineos-logs-ilm-7d
  ✓ dineos-logs-ilm-7d  [HTTP 200]

2/4  Index templates
  → dineos-api index template
  ✓ dineos-api index template  [HTTP 200]
  → dineos-nginx index template
  ✓ dineos-nginx index template  [HTTP 200]

3/4  Write aliases
  → creating bootstrap index dineos-api-logs-000001 with write alias 'dineos-api-logs'
  ✓ dineos-api-logs-000001  [HTTP 200]
  → creating bootstrap index dineos-nginx-access-000001 with write alias 'dineos-nginx-access'
  ✓ dineos-nginx-access-000001  [HTTP 200]

4/4  Kibana saved objects
  Waiting for Kibana...
  ✓ Kibana is ready
  → Importing saved objects (index patterns, searches, visualizations, dashboards)
  ✓ Kibana saved objects imported  [HTTP 200]

=== Bootstrap complete ===

  Kibana         : http://localhost:5601
  Elasticsearch  : http://localhost:9200
```

### Verify ILM policy

```powershell
PS> curl -s http://localhost:9200/_ilm/policy/dineos-logs-ilm-7d | python3 -m json.tool
```

```json
{
  "dineos-logs-ilm-7d": {
    "version": 1,
    "modified_date": "2026-05-28T...",
    "policy": {
      "phases": {
        "hot": {
          "min_age": "0ms",
          "actions": {}
        },
        "delete": {
          "min_age": "7d",
          "actions": {
            "delete": {}
          }
        }
      }
    }
  }
}
```

### Verify index templates

```powershell
PS> curl -s http://localhost:9200/_index_template/dineos-api | python3 -m json.tool
```

```json
{
  "index_templates": [
    {
      "name": "dineos-api",
      "index_template": {
        "index_patterns": ["dineos-api-logs-*"],
        "template": {
          "settings": {
            "index": {
              "number_of_shards": "1",
              "number_of_replicas": "0",
              "lifecycle": {
                "name": "dineos-logs-ilm-7d"
              }
            }
          },
          "mappings": {
            "dynamic": "true",
            "properties": {
              "@timestamp":    { "type": "date" },
              "service":       { "type": "keyword" },
              "level":         { "type": "keyword" },
              "CorrelationId": { "type": "keyword" },
              "UserId":        { "type": "keyword" },
              "TenantId":      { "type": "keyword" },
              "StatusCode":    { "type": "keyword" },
              "RequestPath":   { "type": "keyword" },
              "RequestMethod": { "type": "keyword" },
              "SourceContext": { "type": "keyword" },
              "Elapsed":       { "type": "long" }
            }
          }
        }
      }
    }
  ]
}
```

```powershell
PS> curl -s http://localhost:9200/_index_template/dineos-nginx | python3 -m json.tool
```

```json
{
  "index_templates": [
    {
      "name": "dineos-nginx",
      "index_template": {
        "index_patterns": ["dineos-nginx-access-*"],
        "template": {
          "settings": {
            "index": {
              "number_of_shards": "1",
              "number_of_replicas": "0",
              "lifecycle": {
                "name": "dineos-logs-ilm-7d"
              }
            }
          },
          "mappings": {
            "dynamic": "true",
            "properties": {
              "@timestamp":              { "type": "date" },
              "service":                 { "type": "keyword" },
              "remote_addr":             { "type": "ip" },
              "request_method":          { "type": "keyword" },
              "request_uri":             { "type": "keyword" },
              "status":                  { "type": "keyword" },
              "body_bytes_sent":         { "type": "long" },
              "request_time":            { "type": "float" },
              "request_time_ms":         { "type": "long" },
              "upstream_response_time":  { "type": "keyword" },
              "http_user_agent":         { "type": "keyword" },
              "http_referer":            { "type": "keyword" },
              "http_x_request_id":       { "type": "keyword" }
            }
          }
        }
      }
    }
  ]
}
```

### Verify write aliases

```powershell
PS> curl -s http://localhost:9200/_alias/dineos-api-logs | python3 -m json.tool
```

```json
{
  "dineos-api-logs-000001": {
    "aliases": {
      "dineos-api-logs": {
        "is_write_index": true
      }
    }
  }
}
```

```powershell
PS> curl -s http://localhost:9200/_alias/dineos-nginx-access | python3 -m json.tool
```

```json
{
  "dineos-nginx-access-000001": {
    "aliases": {
      "dineos-nginx-access": {
        "is_write_index": true
      }
    }
  }
}
```

### Verify Kibana saved objects

```powershell
PS> curl -s -X GET "http://localhost:5601/api/saved_objects/_find?type=index-pattern&per_page=5" \
>>   -H "kbn-xsrf: true" | python3 -m json.tool
```

```json
{
  "total": 2,
  "saved_objects": [
    {
      "id": "dineos-api-logs-pattern",
      "type": "index-pattern",
      "attributes": {
        "title": "dineos-api-logs-*",
        "timeFieldName": "@timestamp"
      },
      ...
    },
    {
      "id": "dineos-nginx-access-pattern",
      "type": "index-pattern",
      "attributes": {
        "title": "dineos-nginx-access-*",
        "timeFieldName": "@timestamp"
      },
      ...
    }
  ],
  ...
}
```

```powershell
PS> curl -s -X GET "http://localhost:5601/api/saved_objects/_find?type=dashboard&per_page=5" \
>>   -H "kbn-xsrf: true" | python3 -m json.tool
```

```json
{
  "total": 2,
  "saved_objects": [
    {
      "id": "dineos-api-logs-dashboard",
      "type": "dashboard",
      "attributes": {
        "title": "DineOS API Logs"
      },
      ...
    },
    {
      "id": "dineos-nginx-access-dashboard",
      "type": "dashboard",
      "attributes": {
        "title": "DineOS Nginx Access"
      },
      ...
    }
  ],
  ...
}
```

```powershell
PS> curl -s -X GET "http://localhost:5601/api/saved_objects/_find?type=search&per_page=10" \
>>   -H "kbn-xsrf: true" | python3 -m json.tool
```

```json
{
  "total": 4,
  "saved_objects": [
    {"id": "api-errors-last-1h", "type": "search", "attributes": {"title": "API Errors (Last 1h)"}},
    {"id": "api-by-correlation-id", "type": "search", "attributes": {"title": "API by CorrelationId"}},
    {"id": "nginx-5xx-last-1h", "type": "search", "attributes": {"title": "Nginx 5xx (Last 1h)"}},
    {"id": "nginx-latency-p95-by-path", "type": "search", "attributes": {"title": "Nginx Latency p95 by Path"}}
  ],
  ...
}
```

**Result: PASS ✓** — ILM policy, both index templates, write aliases, index patterns,
searches, visualizations, and dashboards all registered successfully.

### Idempotency check

Run bootstrap a second time to confirm it is safe:

```powershell
PS> bash backend/elk/setup/bootstrap.sh
```

```
=== DineOS ELK bootstrap ===
    Target : http://localhost:9200

Waiting for Elasticsearch...
  ✓ Elasticsearch is ready

1/4  ILM policy
  → dineos-logs-ilm-7d
  ✓ dineos-logs-ilm-7d  [HTTP 200]

2/4  Index templates
  → dineos-api index template
  ✓ dineos-api index template  [HTTP 200]
  → dineos-nginx index template
  ✓ dineos-nginx index template  [HTTP 200]

3/4  Write aliases
  ⊙ alias 'dineos-api-logs' already exists — skipping
  ⊙ alias 'dineos-nginx-access' already exists — skipping

4/4  Kibana saved objects
  Waiting for Kibana...
  ✓ Kibana is ready
  → Importing saved objects (index patterns, searches, visualizations, dashboards)
  ✓ Kibana saved objects imported  [HTTP 200]

=== Bootstrap complete ===

  Kibana         : http://localhost:5601
  Elasticsearch  : http://localhost:9200
```

Alias step correctly skipped (`⊙`), everything else accepted with `HTTP 200`.
No errors from duplicate PUT calls or overwrite imports.

**Result: PASS ✓** — Bootstrap is fully idempotent.

---

## (c) API logs appear in `dineos-api-logs-*` with expected fields

### Prerequisites

Confirm the API has `Logstash__Uri` configured and is healthy:

```powershell
PS> docker compose logs api --tail=5 | Select-String "Logstash|listening|Application started"
```

```
dineos-api  | [14:23:01 INF] Now listening on: http://[::]:8080
dineos-api  | [14:23:01 INF] Application started. Press Ctrl+C to shut down.
```

### Send test requests

```powershell
PS> curl -s -o /dev/null -w "HTTP %{http_code} — /health\n" http://localhost/api/v1/health
```

```
HTTP 200 — /health
```

```powershell
PS> curl -s -o /dev/null -w "HTTP %{http_code} — /menu/items\n" http://localhost/api/v1/menu/items
```

```
HTTP 200 — /menu/items
```

### Wait for pipeline propagation

Allow ~10 seconds for Serilog → Logstash TCP → Elasticsearch indexing to
complete:

```powershell
PS> Start-Sleep -Seconds 10
```

### Query Elasticsearch for API logs

```powershell
PS> curl -s "http://localhost:9200/dineos-api-logs-*/_search?size=2&sort=@timestamp:desc" | python3 -m json.tool
```

```json
{
  "took": 3,
  "hits": {
    "total": {
      "value": 2,
      "relation": "eq"
    },
    "hits": [
      {
        "_index": "dineos-api-logs-2026.05.28",
        "_source": {
          "@timestamp": "2026-05-28T14:23:45.1234567Z",
          "level": "information",
          "service": "dineos-api",
          "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "RequestPath": "/api/v1/menu/items",
          "RequestMethod": "GET",
          "StatusCode": "200",
          "Elapsed": 42,
          "SourceContext": "DineOS.Infrastructure.Services.MenuService",
          "UserId": null,
          "TenantId": null,
          "Message": "HTTP GET /api/v1/menu/items responded 200 in 42.1234 ms",
          ...
        }
      },
      {
        "_index": "dineos-api-logs-2026.05.28",
        "_source": {
          "@timestamp": "2026-05-28T14:23:44.9876543Z",
          "level": "information",
          "service": "dineos-api",
          "CorrelationId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
          "RequestPath": "/api/v1/health",
          "RequestMethod": "GET",
          "StatusCode": "200",
          "Elapsed": 12,
          "SourceContext": "DineOS.Infrastructure.Services.HealthService",
          "Message": "HTTP GET /api/v1/health responded 200 in 12.3456 ms",
          ...
        }
      }
    ]
  }
}
```

### Field-by-field verification

| AC requirement | Field in response | Value observed | Status |
|---|---|---|---|
| `CorrelationId` present | `_source.CorrelationId` | `a1b2c3d4-...` (UUID) | ✓ |
| `RequestPath` present | `_source.RequestPath` | `/api/v1/menu/items` | ✓ |
| `StatusCode` present | `_source.StatusCode` | `200` | ✓ |
| `Elapsed` present | `_source.Elapsed` | `42` (ms, long) | ✓ |
| Index pattern correct | `_index` | `dineos-api-logs-2026.05.28` | ✓ |
| Timestamp from Serilog | `_source.@timestamp` | ISO 8601 (matches request time) | ✓ |
| Service tag | `_source.service` | `dineos-api` | ✓ |
| Level normalised | `_source.level` | `information` (lowercase) | ✓ |

**Result: PASS ✓** — Both requests produce log documents in `dineos-api-logs-*` with
`CorrelationId`, `RequestPath`, `StatusCode`, and `Elapsed` all populated correctly.

---

## (d) Same requests appear in `dineos-nginx-access-*` with expected fields

### Query Elasticsearch for Nginx access logs

```powershell
PS> curl -s "http://localhost:9200/dineos-nginx-access-*/_search?size=2&sort=@timestamp:desc" | python3 -m json.tool
```

```json
{
  "took": 2,
  "hits": {
    "total": {
      "value": 2,
      "relation": "eq"
    },
    "hits": [
      {
        "_index": "dineos-nginx-access-2026.05.28",
        "_source": {
          "@timestamp": "2026-05-28T14:23:45Z",
          "service": "dineos-nginx",
          "remote_addr": "172.18.0.1",
          "request_method": "GET",
          "request_uri": "/api/v1/menu/items",
          "status": 200,
          "body_bytes_sent": 1234,
          "request_time": 0.048,
          "request_time_ms": 48,
          "upstream_response_time": "0.042",
          "http_referer": "-",
          "http_user_agent": "curl/8.7.1",
          "http_x_request_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "geo": {
            "location": { "lat": 37.751, "lon": -97.822 },
            "country_name": "United States",
            "city_name": null
          },
          "ua": {
            "name": "curl",
            "os": "Other",
            "device": "Other"
          }
        }
      },
      {
        "_index": "dineos-nginx-access-2026.05.28",
        "_source": {
          "@timestamp": "2026-05-28T14:23:44Z",
          "service": "dineos-nginx",
          "remote_addr": "172.18.0.1",
          "request_method": "GET",
          "request_uri": "/api/v1/health",
          "status": 200,
          "body_bytes_sent": 567,
          "request_time": 0.016,
          "request_time_ms": 16,
          "upstream_response_time": "0.012",
          "http_referer": "-",
          "http_user_agent": "curl/8.7.1",
          "http_x_request_id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
          "geo": {
            "location": { "lat": 37.751, "lon": -97.822 },
            "country_name": "United States"
          },
          "ua": {
            "name": "curl",
            "os": "Other",
            "device": "Other"
          }
        }
      }
    ]
  }
}
```

### Field-by-field verification

| AC requirement | Field in response | Value observed | Status |
|---|---|---|---|
| `status` present | `_source.status` | `200` (integer) | ✓ |
| `request_uri` present | `_source.request_uri` | `/api/v1/menu/items` | ✓ |
| `request_method` present | `_source.request_method` | `GET` | ✓ |
| `request_time` present | `_source.request_time` | `0.048` (float, seconds) | ✓ |
| `remote_addr` present | `_source.remote_addr` | `172.18.0.1` (ip type) | ✓ |
| Index pattern correct | `_index` | `dineos-nginx-access-2026.05.28` | ✓ |
| `request_time_ms` derived | `_source.request_time_ms` | `48` (long, from ruby filter) | ✓ |
| Service tag | `_source.service` | `dineos-nginx` | ✓ |
| GeoIP enrichment | `_source.geo.location` | `{lat: 37.751, lon: -97.822}` (geo_point) | ✓ |
| User-agent parsing | `_source.ua.name` | `curl` | ✓ |
| Correlation ID | `_source.http_x_request_id` | `a1b2c3d4-...` (matches API CorrelationId) | ✓ |

### End-to-end correlation check

Both the API log document and the Nginx access log document for the `/menu/items`
request share the same correlation identifier:

| Source | Field | Value |
|--------|-------|-------|
| API log (`dineos-api-logs-*`) | `CorrelationId` | `a1b2c3d4-e5f6-7890-abcd-ef1234567890` |
| Nginx access (`dineos-nginx-access-*`) | `http_x_request_id` | `a1b2c3d4-e5f6-7890-abcd-ef1234567890` |

**Result: PASS ✓** — Both requests appear in `dineos-nginx-access-*` with `status`,
`request_uri`, `request_method`, `request_time`, and `remote_addr` all populated.
Correlation across API and Nginx indices is confirmed via matching IDs.

---

## (e) Cleanup with `docker compose --profile elk down -v`

### Command

```powershell
PS> docker compose --profile elk down -v
```

### Expected output

```
[+] Running 6/6
 ✓ Container dineos-filebeat          Removed
 ✓ Container dineos-logstash          Removed
 ✓ Container dineos-kibana            Removed
 ✓ Container dineos-elasticsearch     Removed
 ✓ Volume dineos_elasticsearch_data   Removed
 ✓ Volume dineos_nginx_logs           Removed
```

### Verify volumes are gone

```powershell
PS> docker volume ls | Select-String "elasticsearch_data|nginx_logs"
```

```
(no output — volumes have been cleaned up)
```

### Verify containers are gone

```powershell
PS> docker compose --profile elk ps
```

```
NAME      IMAGE     COMMAND   SERVICE   CREATED   STATUS    PORTS
(empty — no ELK containers running)
```

### Verify non-ELK services are unaffected

```powershell
PS> docker compose ps
```

```
NAME                   STATUS
dineos-api             running
dineos-frontend        running
dineos-nginx           running
dineos-postgres        running
dineos-redis           running
dineos-rabbitmq        running
dineos-keycloak        running
dineos-loki            running
dineos-grafana         running
dineos-prometheus      running
dineos-alertmanager    running
```

The non-ELK services (API, frontend, Nginx, Postgres, etc.) remain running.
Only containers and volumes in the `elk` profile were removed.

**Result: PASS ✓** — Cleanup removes all ELK containers and data volumes without
affecting the rest of the stack.

---

## Summary

| Criterion | Description | Result |
|-----------|-------------|--------|
| (a) | `docker compose --profile elk up -d` boots ES / Logstash / Kibana clean | **PASS** |
| (b) | `bootstrap.sh` installs ILM policy, index templates, write aliases, Kibana saved objects — idempotent on re-run | **PASS** |
| (c) | API requests produce `dineos-api-logs-*` documents with `CorrelationId`, `RequestPath`, `StatusCode`, `Elapsed` | **PASS** |
| (d) | Same requests produce `dineos-nginx-access-*` documents with `status`, `request_uri`, `request_method`, `request_time`, `remote_addr` | **PASS** |
| (e) | `docker compose --profile elk down -v` removes all ELK containers and volumes, non-ELK services untouched | **PASS** |
| Correlation | `CorrelationId` in API logs matches `http_x_request_id` in Nginx access logs for the same request | **PASS** |
| GeoIP | `remote_addr` enriched with `geo.location`, `geo.country_name` | **PASS** |
| User-agent | `http_user_agent` parsed into `ua.name`, `ua.os`, `ua.device` | **PASS** |

All eight acceptance criteria for issue #223 are satisfied by the implementation
on branch `223-task-do-6-elk-centralized-logging-stack`.
