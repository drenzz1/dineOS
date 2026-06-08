# Docker Compose — Full-Stack Dev Environment

## Overview

The repo contains **two** compose files with different scopes:

| File | Purpose |
|------|---------|
| `docker-compose.yml` (repo root) | **Authoritative full-stack dev environment.** Runs every service — API, frontend, Nginx, Postgres, Redis, Keycloak, RabbitMQ, Loki, Grafana, Mailhog. Use this for day-to-day development. |
| `backend/docker-compose.yml` | Backend-only stack (no frontend, no Nginx). Kept for backend developers who want a lighter stack or run the frontend separately with `npm run dev`. |

All commands in this document are run from the **repo root**.

---

## Prerequisites

- **Docker Desktop** (macOS / Windows) or **Docker Engine + Docker Compose v2** (Linux).  
  Run `docker compose version` — you need `v2.x` (the `docker compose` plugin, not the legacy `docker-compose` binary).
- The following host ports must be free. Override any of them in `.env` if something is already listening.

| Variable | Default port | Service |
|----------|-------------|---------|
| `NGINX_HTTP_PORT` | `80` | Nginx (main entry) |
| `FRONTEND_PORT` | `3000` | Next.js (direct) |
| `API_HTTP_PORT` | `5001` | .NET API (direct) |
| `KEYCLOAK_PORT` | `8080` | Keycloak |
| `POSTGRES_PORT` | `5432` | PostgreSQL |
| `REDIS_PORT` | `6379` | Redis |
| `RABBITMQ_AMQP_PORT` | `5672` | RabbitMQ AMQP |
| `RABBITMQ_UI_PORT` | `15672` | RabbitMQ management UI |
| `LOKI_PORT` | `3100` | Loki |
| `GRAFANA_PORT` | `4000` | Grafana |
| `MAILHOG_SMTP_PORT` | `1025` | Mailhog SMTP |
| `MAILHOG_UI_PORT` | `8025` | Mailhog web UI |
| `PGADMIN_PORT` | `5050` | pgAdmin (tools profile) |

---

## First-Time Setup

```bash
# 1. Copy the environment template (only needed once)
cp .env.example .env

# 2. (Optional) Open .env and add Stripe keys, AI provider keys, or override ports.
#    The stack starts and runs without them — billing and AI features degrade gracefully.

# 3. Build images and start all services in the background
docker compose up -d --build
```

The first build takes 3–5 minutes (downloading base images, compiling the .NET API, running `npm ci` + `next build`). Subsequent starts without `--build` are much faster.

---

## Service URL Map

| Service | URL | Notes |
|---------|-----|-------|
| **Frontend (via Nginx)** | http://localhost | Main entry point — all traffic routed through Nginx |
| **Frontend direct** | http://localhost:3000 | Bypasses Nginx; useful for debugging Next.js directly |
| **API (via Nginx)** | http://localhost/api | Proxied by Nginx at `/api/` |
| **API direct** | http://localhost:5001 | Bypasses Nginx |
| **Swagger UI** | http://localhost:5001/swagger | Also reachable via http://localhost/swagger |
| **Keycloak** | http://localhost:8080 | Auth server; realm: `dineos` |
| **RabbitMQ UI** | http://localhost:15672 | Message queue management |
| **Grafana** | http://localhost:4000 | Pre-provisioned with Loki datasource + API dashboard |
| **Loki** | http://localhost:3100 | Log aggregation (internal; also queried by Grafana) |
| **Mailhog** | http://localhost:8025 | Catches all outbound email from the API |
| **pgAdmin** | http://localhost:5050 | Database UI — requires `--profile tools` (see Lifecycle) |

---

## Credentials (Dev Only)

> These are default dev credentials. Do not use them in production.

| Service | Username | Password |
|---------|----------|----------|
| Keycloak admin console | `admin` | `admin` |
| Postgres | `dineos` | `dineos_dev` |
| RabbitMQ | `dineos` | `dineos_dev` |
| Grafana | `admin` | `admin` |
| pgAdmin | `admin@dineos.dev` | `admin` |

**Seeded application users** (Keycloak realm `dineos`, loaded from `backend/keycloak/realm-export.json`):

| Role | Email | Password |
|------|-------|----------|
| Manager | `admin@dineos.dev` | `Test1234!` |
| Cashier | `cashier@dineos.dev` | `Test1234!` |
| Kitchen Staff | `kitchen@dineos.dev` | `Test1234!` |

See `backend/README.md` for the full list of seeded users and roles.

---

## Lifecycle Commands

```bash
# Start all services (uses existing images)
docker compose up -d

# Build images then start (required after code changes)
docker compose up -d --build

# Show running containers and their health status
docker compose ps

# Stream logs for a single service (Ctrl+C to stop)
docker compose logs -f api
docker compose logs -f frontend
docker compose logs -f nginx

# Stop all containers (data volumes are preserved)
docker compose down

# ⚠ DANGER — stop and delete all named volumes
# This wipes Postgres data, Redis cache, RabbitMQ queues, Loki logs, Grafana state.
docker compose down -v

# Start only the pgAdmin service (tools profile)
docker compose --profile tools up -d pgadmin

# Restart a single service
docker compose restart api
```

---

## Healthcheck States

`docker compose ps` shows a `STATUS` column for each container:

| Status | Meaning |
|--------|---------|
| `Up X seconds` | Container running; no healthcheck defined |
| `Up X seconds (healthy)` | Container running and healthcheck passing |
| `Up X seconds (unhealthy)` | Container running but healthcheck failing — check logs |
| `Up X seconds (starting)` | Healthcheck grace period has not yet elapsed |
| `Exited (N)` | Container stopped, exit code N — check logs |

Services with healthchecks: `postgres`, `redis`, `rabbitmq`, `loki`, `api`, `frontend`.  
`nginx` only starts after `api` is healthy and `frontend` is started — if nginx is stuck in `starting`, it is waiting on those dependencies.

To inspect a failing container:

```bash
docker compose logs api        # read the log output
docker inspect dineos-api      # see raw healthcheck results under "Health"
```

---

## Troubleshooting

**Port already in use**  
`Error: address already in use` or `Bind for 0.0.0.0:80 failed`.  
Open `.env` and override the conflicting port variable (e.g. `NGINX_HTTP_PORT=8081`), then re-run `docker compose up -d`.

**Keycloak slow on first boot**  
Keycloak imports the realm on first start (`--import-realm`), which can take 30–60 seconds. The `api` service has `condition: service_started` for Keycloak (not `service_healthy`), so it may log token-validation errors for a minute. Wait for Keycloak to print `Listening on: http://0.0.0.0:8080` in its logs before testing auth flows:

```bash
docker compose logs -f keycloak
```

**Loki not ready**  
Check Loki's readiness endpoint directly:

```bash
curl http://localhost:3100/ready
# expected: "ready"
```

If it returns an error, inspect the logs:

```bash
docker compose logs loki
```

**RabbitMQ healthcheck delay**  
RabbitMQ reports `starting` for up to 90 seconds on first boot while it initialises its Mnesia database. The `api` container will not fully start until RabbitMQ passes its `rabbitmq-diagnostics ping` check. This is normal — do not restart the stack during this window.

**Frontend cannot reach the API**  
If the frontend loads but API calls fail (network errors in the browser console):

1. Check that `NEXT_PUBLIC_API_URL` in your `.env` matches how you are accessing the app.  
   - Full stack via Nginx: `NEXT_PUBLIC_API_URL=http://localhost/api`  
   - Direct API access (bypassing Nginx): `NEXT_PUBLIC_API_URL=http://localhost:5001/api`
2. `NEXT_PUBLIC_API_URL` is baked into the browser bundle **at build time**. If you change it, you must rebuild the frontend image:  
   ```bash
   docker compose up -d --build frontend
   ```
3. Verify the API itself is healthy: `curl http://localhost:5001/api/v1/health`

---

## DO-2 Production Images

Issue **DO-2** introduced production-grade, multi-stage Dockerfiles for the API and frontend.  
Both compose files tag their built images so they can be pushed to a registry without rebuilding.

### Image names and non-root users

| Service | Image tag | Runtime base | Non-root user | UID |
|---------|-----------|--------------|---------------|-----|
| API | `dineos/api:do2` | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` | `appuser` (group `appgroup`) | `1001` |
| Frontend | `dineos/web:do2` | `node:20-alpine` | `nextjs` (group `nodejs`) | `1001` |

The `USER` instruction is set in both Dockerfiles so the process never runs as root inside the container.

### Build commands

Build both images from the **repo root** with a clean layer cache:

```bash
# API  (context: ./backend)
docker build \
  -t dineos/api:do2 \
  -f backend/src/DineOS.Api/Dockerfile \
  ./backend

# Frontend  (context: ./frontend)
docker build \
  -t dineos/web:do2 \
  --build-arg NEXT_PUBLIC_API_URL=http://localhost/api \
  --build-arg NEXT_PUBLIC_KEYCLOAK_URL=http://localhost:8080 \
  --build-arg NEXT_PUBLIC_KEYCLOAK_REALM=dineos \
  ./frontend
```

Or let Compose build and tag both in one step:

```bash
docker compose build --no-cache
```

### Next.js standalone output

`frontend/next.config.ts` sets `output: "standalone"`, which instructs Next.js to emit a self-contained `server.js` bundle with only the node modules it actually uses.  
The runner stage copies three paths from the builder:

| Source (builder) | Destination (runner) | Purpose |
|------------------|----------------------|---------|
| `.next/standalone/` | `./` | Minimal server + trimmed `node_modules` |
| `.next/static/` | `.next/static/` | Hashed CSS / JS chunks |
| `public/` | `public/` | Static assets |

The result is a significantly smaller image compared to copying the full `node_modules` tree.

### Verification

Run the end-to-end verification script from the repo root:

```bash
bash scripts/verify-do2.sh
```

The script performs four checks in order:

| Step | Command | What it verifies |
|------|---------|-----------------|
| 1 | `docker compose build --no-cache` | Clean build succeeds for both images |
| 2 | `docker inspect --format '{{.Config.User}}' dineos/api:do2 dineos/web:do2` | `USER` is set to a non-root identity in both images |
| 3 | `docker compose up -d` | Full stack starts without errors |
| 4 | `curl http://localhost:5001/api/v1/health` | API health endpoint returns 200 after the container passes its healthcheck |

If the API does not become healthy within 90 seconds the script prints the last 30 lines of container logs and exits with a non-zero code.
