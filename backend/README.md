# dineOS Backend

The backend is an ASP.NET Core API for the dineOS restaurant management platform. It owns authentication, tenant isolation, persistence, API versioning, observability, and real-time order updates.

## Stack

- .NET 10 / ASP.NET Core
- EF Core 10 with PostgreSQL via Npgsql
- Keycloak JWT bearer auth
- Redis for refresh-token blacklist and SignalR scale-out
- RabbitMQ for the order-created event flow
- SignalR order update hub
- Serilog console logging with optional Grafana Loki
- xUnit, WebApplicationFactory, EF InMemory, and Testcontainers PostgreSQL

## Solution Layout

```text
backend/
  DineOS.slnx
  docker-compose.yml
  src/
    DineOS.Api/             HTTP host, controllers, middleware, auth, SignalR, appsettings
    DineOS.Application/     DTOs, request models, validators, interfaces, response wrappers
    DineOS.Domain/          Entities, auditing base classes, enums
    DineOS.Infrastructure/  EF DbContext, migrations, repositories, service implementations
  tests/
    DineOS.Tests/           Unit and integration tests
  keycloak/                 Local Keycloak realm export
  grafana/                  Dashboard and datasource provisioning
  loki/                     Loki config
```

Dependency direction:

```text
DineOS.Api -> DineOS.Application + DineOS.Infrastructure
DineOS.Infrastructure -> DineOS.Application + DineOS.Domain
DineOS.Application -> DineOS.Domain
DineOS.Domain -> no project dependencies
```

Keep this direction intact when adding features.

## Running Locally

Run the full Docker stack:

```bash
cd backend
docker compose up --build
```

Useful ports:

| Service | URL |
|---|---|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Keycloak | http://localhost:8080 |
| PostgreSQL | localhost:5432 |
| Redis | localhost:6379 |
| RabbitMQ | http://localhost:15672 |
| Loki | http://localhost:3100 |
| Grafana | http://localhost:4000 |
| pgAdmin | http://localhost:5050 |

Run only dependencies and start the API with the SDK:

```bash
cd backend
docker compose up postgres keycloak redis rabbitmq loki -d
cd src/DineOS.Api
dotnet run
```

The `dotnet run` HTTP launch profile uses `http://localhost:5138`. The Docker API uses `http://localhost:5000`.

Start pgAdmin when needed:

```bash
cd backend
docker compose --profile tools up pgadmin -d
```

## Configuration

Default local configuration lives in:

- `src/DineOS.Api/appsettings.json`
- `src/DineOS.Api/appsettings.Development.json`
- `docker-compose.yml`

Important settings:

| Key | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `AllowedOrigins__0` | Frontend CORS origin, usually `http://localhost:3000` |
| `Keycloak__Authority` | Public realm issuer used for JWT validation |
| `Keycloak__MetadataAddress` | Optional internal OIDC metadata URL |
| `Keycloak__Audience` | Expected access-token audience, currently `dineos-api` |
| `Keycloak__ClientId` | Client used by backend auth endpoints |
| `Keycloak__ClientSecret` | Confidential client secret for non-local deployments |
| `Redis__ConnectionString` | Redis endpoint |
| `RabbitMq__HostName` | RabbitMQ AMQP host (`rabbitmq` in Docker, `localhost` locally) |
| `RabbitMq__UserName` / `RabbitMq__Password` | RabbitMQ credentials |
| `RabbitMq__OrderCreatedQueueName` | Queue used by the order-created notification consumer |
| `Loki__Uri` | Optional Loki sink URL |
| `FileStorage__RootPath` | Filesystem path where uploads are written (`/app/uploads` in Docker) |
| `FileStorage__UrlBasePath` | URL prefix for returned image paths (default `/uploads`) |
| `FileStorage__MaxBytes` | Maximum upload size in bytes (default `5242880` = 5 MB) |
| `FileStorage__ServeLocally` | Set to `true` to serve uploaded files outside Development environment |
| `Stripe__SecretKey` | Stripe test secret key for local billing smoke tests |
| `Stripe__WebhookSecret` | Stripe CLI webhook signing secret from `stripe listen` |
| `Stripe__ProMonthlyPriceId` | Stripe Price ID for the dineOS Pro Monthly test subscription |
| `Stripe__ProAnnualPriceId` | Stripe Price ID for the annual Pro subscription, when available |

## Stripe Local Development

`src/DineOS.Api/appsettings.Development.json` is tracked, so do not put real Stripe
secrets in it. Keep the committed `Stripe` values empty and supply local values
with environment variables or .NET user-secrets:

```bash
cd backend/src/DineOS.Api
dotnet user-secrets init
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
dotnet user-secrets set "Stripe:ProMonthlyPriceId" "price_..."
```

To create the monthly test price in Stripe test mode:

```bash
stripe products create --name "dineOS Pro Monthly"
stripe prices create \
  --currency usd \
  --unit-amount 5000 \
  --recurring interval=month \
  --product prod_...
```

Copy the returned `price_...` value into `Stripe:ProMonthlyPriceId` or
`Stripe__ProMonthlyPriceId`.

Run the local webhook forwarder in a separate terminal and copy the printed
`whsec_...` value into `Stripe:WebhookSecret`:

```bash
stripe listen --forward-to localhost:5001/api/v1/billing/webhook
```

For SDK `dotnet run`, the default HTTP profile uses `http://localhost:5138`, so
forward to that port instead:

```bash
stripe listen --forward-to localhost:5138/api/v1/billing/webhook
```

Use Stripe's successful test card in Checkout:

```text
4242 4242 4242 4242
Any future expiry
Any CVC
Any ZIP
```

Smoke test the subscription flow by starting the API, signing in as a SuperAdmin
tenant user, then calling:

```http
POST /api/v1/billing/checkout-session
Content-Type: application/json

{ "cycle": "Monthly" }
```

The response should contain a real Stripe Checkout URL. Complete the test
payment and confirm the Stripe CLI logs a `200` for the webhook request.

## Auth

Development Keycloak users:

| Email | Password | Role |
|---|---|---|
| admin@dineos.dev | Test1234! | SuperAdmin |
| manager@dineos.dev | Test1234! | Manager |
| cashier@dineos.dev | Test1234! | Cashier |
| kitchen@dineos.dev | Test1234! | KitchenStaff |

Login through the backend:

```bash
curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin@dineos.dev","password":"Test1234!"}'
```

Main auth endpoints:

```text
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
GET  /api/v1/me
```

Authorization policies:

| Policy | Roles |
|---|---|
| `SuperAdminOnly` | SuperAdmin |
| `ManagerAndAbove` | SuperAdmin, Manager |
| `CashierAndAbove` | SuperAdmin, Manager, Cashier |
| `KitchenStaffOnly` | KitchenStaff |

See `../docs/backend/auth.md` for refresh-token rotation and Redis blacklist details.

## API Conventions

- Routes are versioned through URL segments: `/api/v1/...`.
- Controllers return `ApiResponse` envelopes from `DineOS.Application.Common`.
- Validation lives beside request models in `DineOS.Application`.
- Tenant-owned entities should inherit `TenantAuditingEntity`.
- Platform-wide audited entities should inherit `BaseAuditingEntity`.
- Tenant isolation is enforced by `TenantIsolationMiddleware` and EF query filters.
- Swagger is enabled in Development.
- The SignalR order hub is mapped at `/hubs/orders`.

## AI Assistant Rules

Backend Cursor / AI rules live in `./.cursorrules` and cover .NET 10, Clean
Architecture, EF Core + tenant isolation, FluentValidation, Swagger / XML doc
conventions, testing, and structured logging. Read these (and `./AGENTS.md`
for the deeper architectural reference) before generating backend code.

The Swagger enrichment proof for M3.14 lives at
`../docs/backend/ai-assisted-swagger.md` — it documents what AI generated,
what humans reviewed, and what changed on `PaymentsController`.

## Database

Local connection string:

```text
Host=localhost;Port=5432;Database=dineos;Username=dineos;Password=dineos_dev
```

Apply migrations:

```bash
cd backend
dotnet ef database update \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api
```

Add a migration:

```bash
cd backend
dotnet ef migrations add <MigrationName> \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api \
  --output-dir Persistence/Migrations
```

The API also applies pending migrations on startup. See `../docs/database-migrations.md` for the full workflow.

ERD and schema reference (tables, columns, foreign keys, audit/soft-delete columns, migration history):

- `../docs/database/ERD.md` — Mermaid ERD of all tables and relationships
- `../docs/database/SCHEMA.md` — column-level schema, indexes, and migration list
- `../docs/backend/sql-optimization.md` — EXPLAIN ANALYZE proof for the indexes added through `AppDbContext` (orders board, menu by category, recent shift notes, period revenue)
- `../docs/backend/redis-caching.md` — Redis cache-aside on `GET /api/v1/menu/items`: contract, invalidation, and cold-vs-warm benchmark
- `../docs/backend/file-uploads.md` — file upload endpoint: storage path, config keys, validation error codes, filename strategy, static-file serving in dev, Docker volume, and follow-up items
- `../docs/backend/rabbitmq-event-flow.md` — RabbitMQ management UI, topology, queue names, retry/dead-letter behavior, and the order-created event flow

## Testing

Run all backend tests:

```bash
cd backend
dotnet test DineOS.slnx
```

Run a build:

```bash
cd backend
dotnet build DineOS.slnx
```

Test structure:

```text
tests/DineOS.Tests/
  Unit/          Services, middleware, repositories, controllers, auth helpers
  Integration/   API behavior, RBAC, auth errors, tenant isolation, persistence
  Fixtures/      WebApplicationFactory and shared integration setup
  Common/        Shared response wrapper tests
```

## Development Checklist

1. Put request models, DTOs, validators, and service interfaces in `DineOS.Application`.
2. Put entities and enums in `DineOS.Domain`.
3. Put EF configuration, migrations, repositories, and concrete services in `DineOS.Infrastructure`.
4. Put HTTP controllers, middleware, hubs, and API-specific auth behavior in `DineOS.Api`.
5. Preserve tenant isolation and soft-delete query filters.
6. Add focused unit tests and integration tests for cross-layer API behavior.
7. Run `dotnet test DineOS.slnx` before merging backend behavior changes.

## CI/CD

The backend CI workflow (`.github/workflows/backend-ci.yml`) runs on every push and pull request targeting `main`:

- .NET 10 restore, build (Release), and test with `XPlat Code Coverage`
- Coverage gate: the job fails if line coverage falls below **70 %**
- Live Keycloak integration tests run separately on pushes to `main` and `workflow_dispatch`

Docker images for the API are built and pushed to GHCR by `.github/workflows/build-push.yml` on every push to `main` or a `v*.*.*` tag.

See [../docs/devops/cicd.md](../docs/devops/cicd.md) for the full pipeline diagram, required secrets, and troubleshooting.
