# dineOS

dineOS is a restaurant operating system for multi-tenant restaurant management. It combines a .NET backend API with a Next.js frontend for tenant operations, kitchen/order workflows, staff management, menu setup, payments, reports, and platform administration.

## Repository Layout

```text
dineOS/
  backend/      ASP.NET Core API, domain/application/infrastructure projects, tests, Docker services
  frontend/     Next.js App Router client, UI components, typed API clients, tests
  docs/         Architecture notes, auth docs, migration docs, performance/a11y reports
  e2e/          Reserved root-level e2e folder; current Playwright specs live in frontend/e2e
```

## Tech Stack

Backend:

- .NET 10 / ASP.NET Core
- EF Core 10 with PostgreSQL
- Keycloak JWT authentication
- Redis for refresh-token blacklist and SignalR backplane
- Serilog with optional Grafana Loki sink
- xUnit integration and unit tests

Frontend:

- Next.js 16, React 19, TypeScript
- Tailwind CSS 4
- TanStack Query, Zustand, React Hook Form, Zod
- Jest and Playwright

## Prerequisites

- Node.js 20+
- npm
- .NET 10 SDK
- Docker Desktop or another Docker runtime
- Optional: `dotnet-ef` for database migrations

```bash
dotnet tool install --global dotnet-ef
```

## Quick Start

### Option 1 — Full-stack dev environment (DO-1)

Runs every service (API, Next.js frontend, Nginx, Postgres, Keycloak, RabbitMQ, Loki, Grafana, Mailhog) from the repo root in a single command:

```bash
cp .env.example .env
docker compose up -d --build
```

The app is available at **http://localhost** once all services are healthy (~60–90 s on first boot).

| Service | URL |
|---|---|
| App (via Nginx) | http://localhost |
| API | http://localhost/api or http://localhost:5001 |
| Swagger | http://localhost:5001/swagger |
| Keycloak | http://localhost:8080 |
| Grafana | http://localhost:4000 |
| Mailhog | http://localhost:8025 |

See [docs/devops/compose.md](docs/devops/compose.md) for the full service URL map, credentials, lifecycle commands, and troubleshooting.

### Option 2 — Backend-only stack

`backend/docker-compose.yml` still works for backend-only workflows — use this when running the frontend separately with `npm run dev`.

Start the backend stack from `backend/`:

```bash
cd backend
docker compose up --build
```

This starts the API and local dependencies. Main local ports:

| Service | URL |
|---|---|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Keycloak | http://localhost:8080 |
| PostgreSQL | localhost:5432 |
| Redis | localhost:6379 |
| Grafana | http://localhost:4000 |
| Loki | http://localhost:3100 |

Start the frontend from `frontend/`:

```bash
cd frontend
npm ci
npm run dev
```

For real backend API calls, set the API base URL:

```bash
NEXT_PUBLIC_API_URL=http://localhost:5000/api
```

Without this value, the frontend falls back to `/api`, which only works if a proxy or deployment route forwards requests to the backend.

## Local Auth

Keycloak is imported from `backend/keycloak/realm-export.json` when the Docker stack starts.

Seeded development users:

| Email | Password | Role |
|---|---|---|
| admin@dineos.dev | Test1234! | SuperAdmin |
| manager@dineos.dev | Test1234! | Manager |
| cashier@dineos.dev | Test1234! | Cashier |
| kitchen@dineos.dev | Test1234! | KitchenStaff |

The backend exposes auth through:

```text
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
```

The frontend currently still has a development role-picker login flow for route gating. Full Keycloak login wiring on the frontend is still in progress.

## Common Commands

Backend:

```bash
cd backend
dotnet restore
dotnet build DineOS.slnx
dotnet test DineOS.slnx
```

Frontend:

```bash
cd frontend
npm run lint
npx tsc --noEmit
npm test
npm run build
npm run test:e2e
```

Database migrations:

```bash
cd backend
dotnet ef database update \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api
```

See `docs/database-migrations.md` for the full migration workflow.

## Project Notes

- Backend endpoints are versioned as `/api/v{version}/...`; current routes use `/api/v1/...`.
- API responses use the `ApiResponse` envelope and pagination helpers from `DineOS.Application.Common`.
- Tenant isolation is enforced by JWT tenant claims, middleware, and EF query filters.
- Several frontend domains are already wired to typed API clients, while some operational screens still use mocks during backend contract completion.
- CI uses npm for the frontend, even though a `pnpm-lock.yaml` currently exists in the frontend directory. Prefer npm unless the team explicitly switches package managers.

## CI/CD

GitHub Actions handles lint, tests, Docker image builds, and Kubernetes deployments automatically.

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| Frontend CI (`ci.yml`) | push + PR | Lint, Jest, Playwright, Next.js build artifact |
| Backend CI (`backend-ci.yml`) | push / PR to `main` | .NET 10 build, tests, coverage gate |
| Helm CI (`helm.yml`) | changes to `deploy/helm/**` | Helm lint + kubeconform schema validation |
| Observability CI (`observability.yml`) | changes to `backend/prometheus/**` | promtool config/rules + amtool config check |
| Build & Push (`build-push.yml`) | push to `main`, `v*.*.*` tags | Docker build → GHCR push → Helm deploy → notify |
| Commitlint (`commitlint.yml`) | PR | Validate all PR commits + PR title follow Conventional Commits |
| Release Please (`release-please.yml`) | push to `main` | Open release PR; on merge create `v*.*.*` tag + GitHub Release |

### Required secrets

| Secret | Required | Description |
|--------|----------|-------------|
| `GITHUB_TOKEN` | Automatic | Provided by GitHub Actions. Grants `packages: write` for GHCR push. |
| `KUBE_CONFIG_DATA` | Optional | base64-encoded kubeconfig. If absent the deploy job runs `--dry-run` only. |
| `SLACK_WEBHOOK_URL` | Optional | Enables Slack notifications on build completion. |
| `NEXT_PUBLIC_API_URL` | Optional | Baked into the frontend image at build time. Defaults to `http://localhost/api`. |
| `NEXT_PUBLIC_KEYCLOAK_URL` | Optional | Baked into the frontend image. Defaults to `http://localhost:8080`. |
| `NEXT_PUBLIC_KEYCLOAK_REALM` | Optional | Baked into the frontend image. Defaults to `dineos`. |

See [docs/devops/cicd.md](docs/devops/cicd.md) for the pipeline diagram, `gh secret set` setup commands, how to configure the `production` GitHub Environment with required reviewers, and troubleshooting.

## Observability

Prometheus scrapes `/metrics` from the API every 15 s; eight alert rules (API error rate, latency p95, GC pause, database, queue backlog, disk, memory) route through Alertmanager to Slack. Three Grafana dashboards — **API Overview**, **.NET Runtime**, and **Infrastructure** — sit alongside the existing Loki log dashboard. All services start with `docker compose up -d`; run `bash scripts/demo-alert.sh` to watch the `ApiDown` alert fire and self-clear in under two minutes. In Kubernetes, flip `observability.prometheus.enabled=true` and `observability.alertmanager.enabled=true` in the Helm chart.

See [docs/devops/observability.md](docs/devops/observability.md) for the architecture diagram, alert rationale, Prometheus vs Loki guidance, and full Kubernetes setup.
An optional **ELK stack** (`--profile elk`) adds Elasticsearch + Logstash + Kibana with pre-built dashboards for API logs and Nginx access analytics — see [docs/devops/elk.md](docs/devops/elk.md).
An optional **Uptime Kuma** instance (`--profile uptime`) adds synthetic black-box monitoring and a public status page — seven HTTP/TCP/keyword monitors cover every service, with Slack and email notifications sharing the same `SLACK_WEBHOOK_URL` used by Alertmanager. Run `bash scripts/demo-uptime-kuma.sh` to watch a DOWN alert and recovery email arrive in Mailhog end-to-end. In Kubernetes, flip `observability.uptimeKuma.enabled=true` in the Helm chart — see [docs/devops/uptime-kuma.md](docs/devops/uptime-kuma.md).

Alertmanager routes all firing alerts to the **dineOS backend webhook** (`POST /api/v1/alerts/webhook`) rather than posting raw payloads directly to Slack. The backend runs AI triage (Anthropic / OpenAI / Google) — producing severity, likely causes, suggested next actions, and a short summary — then posts a structured Block Kit message to Slack via `SlackNotifier`. Every step is failure-isolated; Alertmanager always receives `200 OK`. The original Alertmanager → Slack path is preserved as a `slack-direct` receiver that can be toggled on instantly. Run `bash scripts/demo-do12.sh` to post a synthetic alert and watch the full triage pipeline. See [docs/devops/aiops-triage.md](docs/devops/aiops-triage.md) for the architecture diagram, config/secrets reference, demo steps, and failure-path behavior.

## Security

Trivy scans dependency manifests on every pull request (`trivy fs`) and the built Docker images after every push to `main` (`trivy image`). Both scans gate on CRITICAL/HIGH unfixed CVEs and fail CI with exit code 1 before a vulnerable image can reach the `deploy` job. Both runtime images run as a non-root UID 1001 user on Alpine-based minimal bases. Justified exceptions are documented in `.trivyignore` with an explicit expiry date; Trivy enforces the expiry natively so suppressions cannot silently persist beyond their review window.

See [docs/devops/security.md](docs/devops/security.md) for the image hardening criteria with Dockerfile references, how to reproduce scans locally with `trivy fs` and `trivy image`, and the full `.trivyignore` add/review/expiry workflow.

## More Documentation

- Backend details: `backend/README.md`
- Frontend details: `frontend/README.md`
- Keycloak setup: `docs/keycloak-setup.md`
- Backend auth design: `docs/backend/auth.md`
- Database migrations: `docs/database-migrations.md`
- Database ERD: `docs/database/ERD.md`
- Database schema reference: `docs/database/SCHEMA.md`
- API client strategy: `frontend/docs/api-client-strategy.md`
- Docker Compose dev environment: `docs/devops/compose.md`
- Helm / Kubernetes deployment: `docs/devops/helm.md`
- CI/CD pipeline: `docs/devops/cicd.md`
- Observability (Prometheus, Alertmanager, Grafana): `docs/devops/observability.md`
- Observability (ELK centralized logging): `docs/devops/elk.md`
- Observability (Uptime Kuma status page): `docs/devops/uptime-kuma.md`
- AI-powered incident triage (DO-12): `docs/devops/aiops-triage.md`
- Container security (image hardening, Trivy scanning): `docs/devops/security.md`
- Release workflow (Conventional Commits, release-please, semver): `docs/devops/releases.md`
