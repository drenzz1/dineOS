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
